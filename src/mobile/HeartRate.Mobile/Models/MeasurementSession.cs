namespace HeartRate.Mobile.Models;

public sealed record MeasurementSession(
    string Id,
    string Name,
    string Source,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc);
