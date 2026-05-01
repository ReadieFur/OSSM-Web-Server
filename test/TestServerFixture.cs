using Microsoft.AspNetCore.Builder;
using System.Net;
using System.Net.Sockets;

namespace OSSMWebServer.Test
{
    public sealed class TestServerFixture : IAsyncLifetime
    {
        public WebApplication App { get; private set; } = null!;
        public string HttpUrl { get; private set; } = "";
        public int Port { get; private set; }
        public RoomManager RoomManager => typeof(Program).GetField("DbgRoomManager", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)!.GetValue(null) as RoomManager ?? throw new NullReferenceException();

        public async Task InitializeAsync()
        {
            Port = GetFreePort();
            HttpUrl = $"http://127.0.0.1:{Port}";

            App = Program.BuildApp([]);

            _ = App.RunAsync(HttpUrl)
                .ContinueWith(t =>
                {
                    if (t.Exception is not null)
                        throw t.Exception;
                }, TaskContinuationOptions.OnlyOnFaulted);

            // small delay to ensure binding is ready
            await Task.Delay(200);
        }

        public async Task DisposeAsync()
        {
            await App.StopAsync();
        }

        private static int GetFreePort()
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
