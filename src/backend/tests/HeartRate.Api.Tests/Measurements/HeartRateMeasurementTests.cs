using HeartRate.Domain.Entities;

namespace HeartRate.Api.Tests.Measurements;

public sealed class HeartRateMeasurementTests
{
    [Fact]
    public void Create_WhenBpmIsOutsideAcceptedRange_ThrowsException()
    {
        var action = () => Measurement.Create(
            "measurement-id",
            "user-id",
            "session-id",
            DateTime.UtcNow,
            251);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }
}
