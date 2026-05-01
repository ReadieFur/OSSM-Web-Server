using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace OSSMWebServer.Test
{
    public class TestServer : IClassFixture<TestServerFixture>
    {
        private readonly TestServerFixture _server;

        public TestServer(TestServerFixture fixture)
        {
            _server = fixture;
        }

        private static CancellationToken CreateTimeoutToken(int milliseconds = 5000)
        {
            CancellationTokenSource cts = new();
            cts.CancelAfter(milliseconds);
            return cts.Token;
        }

        private async Task<ClientWebSocket> CreateClient()
        {
            ClientWebSocket client = new();
            await client.ConnectAsync(new Uri($"ws://127.0.0.1:{_server.Port}/ws"), CreateTimeoutToken());
            Assert.Equal(WebSocketState.Open, client.State);
            return client;
        }

        private async Task SendClient(ClientWebSocket client, object body)
        {
            string json = JsonSerializer.Serialize(body);
            await client.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CreateTimeoutToken());
        }

        private async Task<JsonDocument> ReceiveClient(ClientWebSocket client)
        {
            byte[] recvBuffer = new byte[4096];
            WebSocketReceiveResult result = await client.ReceiveAsync(recvBuffer, CreateTimeoutToken());
            string recvJson = Encoding.UTF8.GetString(recvBuffer, 0, result.Count);
            return JsonDocument.Parse(recvJson);
        }

        [Fact]
        public async Task Connect_AndCreateRoom_ShouldReturnRoomId()
        {
            using ClientWebSocket client = await CreateClient();

            await SendClient(client, new { command = ECommand.CreateRoom });

            JsonDocument recvDoc = await ReceiveClient(client);
            AssertJsonDoc.Satisfies(new
            {
                command = ECommand.CreateRoom,
                roomId = (Func<string, bool>)(s => !string.IsNullOrEmpty(s))
            }, recvDoc.RootElement);
        }

        [Fact]
        public async Task HostApproveGuest_ShouldAllowGuestIntoRoom()
        {
            // Create clients
            using ClientWebSocket host = await CreateClient();
            using ClientWebSocket guest = await CreateClient();

            // Host creates room
            await SendClient(host, new { command = ECommand.CreateRoom });
            JsonDocument hostCreateRoomRecvDoc = await ReceiveClient(host);
            AssertJsonDoc.Satisfies(new { roomId = (Func<string, bool>)(s => !string.IsNullOrWhiteSpace(s)) }, hostCreateRoomRecvDoc.RootElement);
            string roomId = hostCreateRoomRecvDoc.RootElement.GetProperty("roomId").GetString()!;

            // Guest requests to join room and recieves pending message
            await SendClient(guest, new { command = ECommand.JoinRoom, roomId });
            AssertJsonDoc.Satisfies(new { command = ECommand.JoinRoom, status = EStatus.Pending }, (await ReceiveClient(guest)).RootElement);

            // Server should have a pending join request
            if (typeof(RoomManager).GetField("_pendingJoinRequests", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(_server.RoomManager) is not ConcurrentDictionary<Client, Room> pendingRequests)
                throw new InvalidCastException();
            if (pendingRequests.First().Value.Id != roomId)
                throw new InvalidOperationException("Pending join request does not match the created room.");

            // Host receives join request, accept it
            JsonDocument hostJoinRequestDoc = await ReceiveClient(host);
            AssertJsonDoc.Satisfies(new
            {
                command = ECommand.JoinRequest,
                clientId = (Func<string, bool>)(s => !string.IsNullOrWhiteSpace(s)),
            }, hostJoinRequestDoc.RootElement);
            string guestId = hostJoinRequestDoc.RootElement.GetProperty("clientId").GetString()!;
            await SendClient(host, new { command = ECommand.JoinRequest, clientId = guestId, approved = true });

            // Guest receives approval
            AssertJsonDoc.Satisfies(new
            {
                command = ECommand.JoinRequest,
                roomId,
                status = EStatus.Accepted
            }, (await ReceiveClient(guest)).RootElement);
        }

        [Fact]
        public async Task HostDenyGuest_ShouldntAllowGuestIntoRoom()
        {
            // Create clients
            using ClientWebSocket host = await CreateClient();
            using ClientWebSocket guest = await CreateClient();

            // Host creates room
            await SendClient(host, new { command = ECommand.CreateRoom });
            string roomId = (await ReceiveClient(host)).RootElement.GetProperty("roomId").GetString()!;

            // Guest requests to join room
            await SendClient(guest, new { command = ECommand.JoinRoom, roomId });
            _ = await ReceiveClient(guest);

            // Host receives join request, deny it
            string guestId = (await ReceiveClient(host)).RootElement.GetProperty("clientId").GetString()!;
            await SendClient(host, new { command = ECommand.JoinRequest, clientId = guestId, approved = false });

            // Guest receives denial
            AssertJsonDoc.Satisfies(new
            {
                command = ECommand.JoinRequest,
                roomId,
                status = EStatus.Rejected
            }, (await ReceiveClient(guest)).RootElement);
        }

        [Fact]
        public async Task HostLeavesRoom_ShouldNotifyGuests()
        {
            // Create clients
            using ClientWebSocket host = await CreateClient();
            using ClientWebSocket guest = await CreateClient();

            // Host creates room
            await SendClient(host, new { command = ECommand.CreateRoom });
            string roomId = (await ReceiveClient(host)).RootElement.GetProperty("roomId").GetString()!;

            // Guest requests to join room
            await SendClient(guest, new { command = ECommand.JoinRoom, roomId });
            _ = await ReceiveClient(guest);

            // Host receives join request, accept it
            string guestId = (await ReceiveClient(host)).RootElement.GetProperty("clientId").GetString()!;
            await SendClient(host, new { command = ECommand.JoinRequest, clientId = guestId, approved = true });
            _ = await ReceiveClient(guest);

            // Host leaves the room
            await SendClient(host, new { command = ECommand.LeaveRoom });

            // Guest receives notification that host has left
            AssertJsonDoc.Satisfies(new
            {
                command = ECommand.LeaveRoom,
                message = "Host has left the room"
            }, (await ReceiveClient(guest)).RootElement);

            // Verify the server has removed the room
            if (typeof(RoomManager).GetField("_rooms", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(_server.RoomManager) is not ConcurrentDictionary<string, Room> rooms)
                throw new InvalidCastException();
            Assert.False(rooms.ContainsKey(roomId), "Room was not removed after host left.");
        }

        [Fact]
        public async Task ClientLeavesRoom_ShouldNotifyHost()
        {
            // Create clients
            using ClientWebSocket host = await CreateClient();
            using ClientWebSocket guest = await CreateClient();

            // Host creates room
            await SendClient(host, new { command = ECommand.CreateRoom });
            string roomId = (await ReceiveClient(host)).RootElement.GetProperty("roomId").GetString()!;

            if (typeof(RoomManager).GetField("_rooms", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(_server.RoomManager) is not ConcurrentDictionary<string, Room> rooms)
                throw new InvalidCastException();
            Room room = rooms[roomId];

            // Guest requests to join room
            await SendClient(guest, new { command = ECommand.JoinRoom, roomId });
            _ = await ReceiveClient(guest);

            // Host receives join request, accept it
            string guestId = (await ReceiveClient(host)).RootElement.GetProperty("clientId").GetString()!;
            await SendClient(host, new { command = ECommand.JoinRequest, clientId = guestId, approved = true });
            _ = await ReceiveClient(guest);

            Client internalClient = room.Guests.FirstOrDefault(c => c.Id == Guid.Parse(guestId)) ?? throw new InvalidOperationException("Guest not found in room.");

            // Guest leaves the room
            await SendClient(guest, new { command = ECommand.LeaveRoom });

            // Host receives notification that guest has left
            AssertJsonDoc.Satisfies(new
            {
                command = ECommand.LeaveRoom,
                clientId = guestId,
                message = "Guest has left the room"
            }, (await ReceiveClient(host)).RootElement);

            // Verify the server has removed the guest from the room
            Assert.DoesNotContain(internalClient, room.Guests);
        }
    }
}
