using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace OSSMWebServer
{
    public class Connection : Client
    {
        public required WebSocket Socket { private get; init; }

        public override Task NotifyAsync(object payload) => SendAsync(payload);

        public async Task SendAsync(object payload)
        {
            string json = JsonSerializer.Serialize(payload);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await Socket.SendAsync(
                bytes,
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
    }
}
