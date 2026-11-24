namespace HeartRate.Domain.Entities;

public sealed class Measurement
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string UserId { get; set; }
    public string? DeviceId { get; set; }
    public string? SessionId { get; set; }
    public DateTime MeasuredAtUtc { get; set; }
    public float Bpm { get; set; }
    // Bez sesji: średnia z 10 sekund. W sesji: próbka z 1 sekundy.
    public int IntervalSeconds => SessionId is null ? 10 : 1;

    public static Measurement Create(
        string id, string userId, string? sessionId, DateTime measuredAtUtc, float bpm)
    {
        if (!float.IsFinite(bpm) || bpm is < 30 or > 250)
            throw new ArgumentOutOfRangeException(nameof(bpm), "BPM musi być w zakresie 30–250.");

        return new Measurement
        {
            Id = id, UserId = userId, SessionId = sessionId,
            MeasuredAtUtc = measuredAtUtc, Bpm = bpm
        };
    }
}
