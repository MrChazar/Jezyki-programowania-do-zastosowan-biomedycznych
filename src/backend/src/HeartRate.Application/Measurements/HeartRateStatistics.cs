using HeartRate.Domain.Entities;

namespace HeartRate.Application.Measurements;

public sealed record HeartRateStatistics(
    int MeasurementCount,
    float? MinimumBpm,
    float? MaximumBpm,
    double? AverageBpm,
    DateTime? PeakAtUtc)
{
    public static HeartRateStatistics Calculate(IEnumerable<Measurement> measurements)
    {
        var items = measurements.ToList();
        if (items.Count == 0)
            return new(0, null, null, null, null);

        var peak = items.MaxBy(x => x.Bpm)!;
        var average = items.Sum(x => (double)x.Bpm * x.IntervalSeconds)
            / items.Sum(x => x.IntervalSeconds);

        return new(items.Count, items.Min(x => x.Bpm), peak.Bpm,
            Math.Round(average, 2), peak.MeasuredAtUtc);
    }
}
