using HeartRate.Application.Measurements;
using HeartRate.Domain.Entities;

namespace HeartRate.Api.Tests.Measurements;

public sealed class HeartRateStatisticsTests
{
    [Fact]
    public void Calculate_WithMeasurements_ReturnsMinimumMaximumAndAverage()
    {
        var measurements = new[]
        {
            CreateMeasurement("1", 72),
            CreateMeasurement("2", 84),
            CreateMeasurement("3", 96)
        };

        var result = HeartRateStatistics.Calculate(measurements);

        Assert.Equal(3, result.MeasurementCount);
        Assert.Equal(72, result.MinimumBpm);
        Assert.Equal(96, result.MaximumBpm);
        Assert.Equal(84, result.AverageBpm);
    }

    [Fact]
    public void Calculate_WithoutMeasurements_ReturnsEmptyStatistics()
    {
        var result = HeartRateStatistics.Calculate([]);

        Assert.Equal(0, result.MeasurementCount);
        Assert.Null(result.MinimumBpm);
        Assert.Null(result.MaximumBpm);
        Assert.Null(result.AverageBpm);
    }

    [Fact]
    public void Calculate_WithMixedIntervals_WeightsAverageBySampleDurationAndReturnsPeakTime()
    {
        var time = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
        var ordinary = Measurement.Create("ordinary", "user", null, time, 60);
        var session = Measurement.Create("session", "user", "session-id", time.AddSeconds(10), 120);

        var result = HeartRateStatistics.Calculate([ordinary, session]);

        Assert.Equal(65.45, result.AverageBpm);
        Assert.Equal(session.MeasuredAtUtc, result.PeakAtUtc);
    }

    private static Measurement CreateMeasurement(string id, int bpm) =>
        Measurement.Create(id, "user-id", "session-id", DateTime.UtcNow, bpm);
}
