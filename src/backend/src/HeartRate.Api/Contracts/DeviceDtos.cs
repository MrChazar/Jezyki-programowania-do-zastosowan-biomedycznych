using HeartRate.Domain.Entities;

namespace HeartRate.Api.Contracts;

public sealed record PairDeviceRequest(string? DeviceId, string? PairingCode);

public sealed record DeviceResponse(string Id, string Name, bool IsActive, DateTime? LastSeenAtUtc)
{
    public static DeviceResponse From(Device device) => new(
        device.Id, device.Name, device.IsActive, device.LastSeenAtUtc);
}
