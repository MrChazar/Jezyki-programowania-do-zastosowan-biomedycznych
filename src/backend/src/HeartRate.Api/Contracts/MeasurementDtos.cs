using HeartRate.Application.Measurements;
using HeartRate.Domain.Entities;

namespace HeartRate.Api.Contracts;

public sealed record MeasurementResponse(
    string Id, string? SessionId, string? DeviceId,
    DateTime MeasuredAtUtc, float Bpm, int IntervalSeconds)
{
    public static MeasurementResponse From(Measurement measurement) => new(
        measurement.Id, measurement.SessionId, measurement.DeviceId,
        measurement.MeasuredAtUtc, measurement.Bpm, measurement.IntervalSeconds);
}

public sealed record MeasurementDateResponse(DateOnly Date, IReadOnlyList<string> MeasurementIds);
public sealed record MeasurementIdResponse(string Id, DateTime MeasuredAtUtc, string? SessionId)
{
    public static MeasurementIdResponse From(Measurement measurement) =>
        new(measurement.Id, measurement.MeasuredAtUtc, measurement.SessionId);
}
public sealed record MonthlyStatisticsResponse(
    DateTime FromUtc, DateTime ToUtc, HeartRateStatistics Statistics);
