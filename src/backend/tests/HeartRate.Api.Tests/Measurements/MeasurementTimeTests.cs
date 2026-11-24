using HeartRate.Application.Measurements;

namespace HeartRate.Api.Tests.Measurements;

public sealed class MeasurementTimeTests
{
    [Theory]
    [InlineData(2026, 3, 29, 23)]
    [InlineData(2026, 10, 25, 25)]
    [InlineData(2026, 11, 17, 24)]
    public void DayUtcRange_UsesLocalMidnightsAndDaylightSavingTime(int year, int month, int day, int hours)
    {
        var date = new DateOnly(year, month, day);
        var (start, end) = MeasurementTime.DayUtcRange(date, "Europe/Warsaw");
        Assert.Equal(TimeSpan.FromHours(hours), end - start);
        Assert.Equal(date, MeasurementTime.LocalDate(start, "Europe/Warsaw"));
        Assert.Equal(date, MeasurementTime.LocalDate(end.AddTicks(-1), "Europe/Warsaw"));
    }
}
