using HeartRate.Domain.Entities;

namespace HeartRate.Api.Contracts;

public sealed record UpdateSessionRequest(string? Name = null, List<string>? Tags = null);

public sealed record SessionResponse(
    string Id, string Name, string? DeviceId, string Source,
    DateTime StartedAtUtc, DateTime? FinishedAtUtc, IReadOnlyList<string> Tags)
{
    public static SessionResponse From(Session session) => new(
        session.Id, session.Name, session.DeviceId, session.Source,
        session.StartedAtUtc, session.FinishedAtUtc, session.Tags);
}
