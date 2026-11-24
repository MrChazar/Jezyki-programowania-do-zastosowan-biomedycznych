namespace HeartRate.Domain.Entities;

public sealed class Device
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? UserId { get; set; }
    public required string Name { get; set; }
    public string? PairingCodeHash { get; set; }
    public DateTime? PairingCodeExpiresAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSeenAtUtc { get; set; }
}
