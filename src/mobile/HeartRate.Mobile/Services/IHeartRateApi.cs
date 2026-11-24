using HeartRate.Mobile.Models;

namespace HeartRate.Mobile.Services;

public interface IHeartRateApi
{
    Task<IReadOnlyList<MeasurementSession>> GetSessionsAsync(
        CancellationToken cancellationToken = default);
}
