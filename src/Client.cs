namespace OSSMWebServer
{
    public abstract class Client
    {
        public Guid Id { get; } = Guid.NewGuid();
        //public string? Name { get; set; }
        public Room? ActiveRoom { get; set; }

        public abstract Task NotifyAsync(object payload);
    }
}
