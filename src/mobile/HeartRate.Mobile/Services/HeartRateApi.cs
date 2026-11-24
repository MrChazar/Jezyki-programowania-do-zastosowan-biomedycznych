using System.Net.Http.Json;
using HeartRate.Mobile.Models;

namespace HeartRate.Mobile.Services;

public sealed class HeartRateApi(HttpClient httpClient) : IHeartRateApi
{
    public async Task<IReadOnlyList<MeasurementSession>> GetSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        var sessions = await httpClient.GetFromJsonAsync<List<MeasurementSession>>(
            "api/sessions",
            cancellationToken);

        return sessions ?? [];
    }
}
