using OSSMWebServer;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json;

// Main entry point for the application
WebApplication app = BuildApp(args);
app.Run();

// Partial class for unit test accessibility
public partial class Program
{
    public static readonly object APP_INFO = new
    {
        name = Assembly.GetEntryAssembly()!.GetName().Name,
        version = Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Assembly.GetEntryAssembly()!.GetName().Version?.ToString(),
    };

#if DEBUG
    public static RoomManager? DbgRoomManager;
#endif

    public static WebApplication BuildApp(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        WebApplication app = builder.Build();

        app.UseWebSockets();

        RoomManager roomManager = new();
#if DEBUG
        DbgRoomManager = roomManager;
#endif

        app.MapGet("/ws/info", () => APP_INFO);

        app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
            Connection connection = new Connection() { Socket = socket };
            roomManager.RegisterClient(connection);

            byte[] buffer = new byte[4096];

            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult websocketRecvResult;
                try
                {
                    websocketRecvResult = await socket.ReceiveAsync(buffer, context.RequestAborted);
                    if (websocketRecvResult.MessageType == WebSocketMessageType.Close)
                        break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                    continue;
                }

                JsonDocument json;
                try
                {
                    string jsonStr = Encoding.UTF8.GetString(buffer, 0, websocketRecvResult.Count);
                    json = JsonDocument.Parse(jsonStr);
                }
                catch (JsonException)
                {
                    continue;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                    continue;
                }

                if (!json.RootElement.TryGetProperty("command", out JsonElement _))
                {
                    await connection.SendAsync(new { command = ECommand.UnknownError, message = "Missing command property" });
                    continue;
                }
                if (!json.RootElement.GetProperty("command").TryGetInt32(out int requestCmdInt) || !Enum.IsDefined(typeof(ECommand), requestCmdInt))
                {
                    await connection.SendAsync(new { command = ECommand.UnknownError, message = "Requested command not recognised" });
                    continue;
                }
                ECommand requestCommand = (ECommand)requestCmdInt;

                try
                {
                    switch (requestCommand)
                    {
                        #region Host
                        case ECommand.CreateRoom:
                            {
                                Room room = roomManager.CreateRoom(connection);

                                await connection.SendAsync(new
                                {
                                    command = ECommand.CreateRoom,
                                    roomId = room.Id
                                    // TODO: Add transaction ID to all messages?
                                });
                                break;
                            }

                        case ECommand.JoinRequest:
                            {
                                if (connection.ActiveRoom is null)
                                    throw new InvalidOperationException("You are not the host of a room");

                                if (!json.RootElement.GetProperty("clientId").TryGetGuid(out Guid clientId))
                                    throw new Exception("clientId is required");

                                bool approved = json.RootElement.GetProperty("approved").GetBoolean();

                                if (!roomManager.TryGetClient(clientId, out Client guest))
                                    throw new Exception("Guest not found");

                                await roomManager.ResolveJoinRequest(connection, connection.ActiveRoom, guest, approved);

                                // Echo back to the host that the request has been processed
                                //await connection.SendAsync(new
                                //{
                                //    command = ECommand.JoinRequest,
                                //    clientId = guest.Id,
                                //    status = approved ? EStatus.Accepted : EStatus.Rejected
                                //});

                                break;
                            }
                        #endregion

                        #region Guest
                        case ECommand.JoinRoom:
                            {
                                if (json.RootElement.GetProperty("roomId").GetString() is not string roomId)
                                    throw new ArgumentException("roomId is required");

                                if (!roomManager.TryGetRoom(roomId, out Room room))
                                    throw new KeyNotFoundException("Room not found");

                                await roomManager.CreateJoinRequest(room, connection);

                                await connection.SendAsync(new
                                {
                                    command = ECommand.JoinRoom,
                                    roomId = room.Id,
                                    status = EStatus.Pending
                                });

                                break;
                            }
                        #endregion

                        #region Both
                        case ECommand.LeaveRoom:
                            {
                                if (connection.ActiveRoom is null)
                                    throw new InvalidOperationException("You are not in a room");
                                await roomManager.LeaveRoom(connection);
                                break;
                            }
                        #endregion
                    }
                }
                catch (Exception ex)
                {
                    await connection.SendAsync(new { command = requestCommand, error = ex.GetType().Name, message = ex.Message });
                }
            }

            await roomManager.UnregisterClient(connection);
        });

        return app;
    }
}
