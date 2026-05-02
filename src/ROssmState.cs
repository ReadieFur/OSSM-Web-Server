using System.Text.Json.Serialization;

namespace OSSMWebServer
{
    public record ROssmState
    {
        [JsonPropertyName("status")] public string Status { get; set; } = "error";
        [JsonPropertyName ("speed")] public int Speed { get; set; }
        [JsonPropertyName("stroke")] public int Stroke { get; set; }
        [JsonPropertyName("sensation")] public int Sensation { get; set; }
        [JsonPropertyName("depth")] public int Depth { get; set; }
        [JsonPropertyName("pattern")] public int Pattern { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Status) || Status.Length > 32)
                throw new ArgumentException(nameof(Status));
            if (Speed < 0 || Speed > 100)
                throw new ArgumentOutOfRangeException(nameof(Speed));
            if (Stroke < 0 || Stroke > 100)
                throw new ArgumentOutOfRangeException(nameof(Stroke));
            if (Sensation < 0 || Sensation > 100)
                throw new ArgumentOutOfRangeException(nameof(Sensation));
            if (Depth < 0 || Depth > 100)
                throw new ArgumentOutOfRangeException(nameof(Depth));
            if (Pattern < 0)
                throw new ArgumentOutOfRangeException(nameof(Pattern), "Pattern cannot be negative");
        }
    }
}
