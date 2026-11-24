namespace HeartRate.Domain.Entities;

public sealed class Session
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string UserId { get; set; }
    public string? DeviceId { get; set; }
    public required string Name { get; set; }
    public string Source { get; set; } = "Sensor";
    public List<string> Tags { get; set; } = [];
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
}
