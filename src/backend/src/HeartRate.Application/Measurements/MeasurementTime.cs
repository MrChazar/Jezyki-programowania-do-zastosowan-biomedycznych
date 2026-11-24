namespace HeartRate.Application.Measurements;

public static class MeasurementTime
{
    public static (DateTime Start, DateTime End) DayUtcRange(DateOnly date, string timeZone)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        return (ToUtc(date, zone), ToUtc(date.AddDays(1), zone));
    }

    public static DateOnly LocalDate(DateTime utc, string timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            TimeZoneInfo.FindSystemTimeZoneById(timeZone)));

    private static DateTime ToUtc(DateOnly date, TimeZoneInfo zone)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue);
        // Niektóre strefy zmieniają czas o północy.
        while (zone.IsInvalidTime(midnight))
            midnight = midnight.AddMinutes(1);

        return TimeZoneInfo.ConvertTimeToUtc(midnight, zone);
    }
}
