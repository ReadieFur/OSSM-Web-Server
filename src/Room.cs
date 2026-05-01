namespace OSSMWebServer
{
    public class Room
    {
        public required string Id { get; init; }
        public required Client Host { get; init; }
        public HashSet<Client> Guests { get; } = new();
    }
}
