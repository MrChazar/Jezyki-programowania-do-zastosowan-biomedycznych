using HeartRate.Domain.Entities;

namespace HeartRate.Api.Contracts;

public sealed record UpdateProfileRequest(
    string? Name = null, string? Surname = null, DateOnly? DateOfBirth = null, string? TimeZone = null);

public sealed record UserResponse(
    string Id, string Name, string Surname, string Email,
    DateOnly? DateOfBirth, string TimeZone, DateTime CreatedAtUtc)
{
    public static UserResponse From(User user) => new(
        user.Id, user.Name, user.Surname, user.Email,
        user.DateOfBirth, user.TimeZone, user.CreatedAtUtc);
}
