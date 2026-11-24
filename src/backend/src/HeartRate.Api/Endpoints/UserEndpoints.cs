using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using HeartRate.Api.Contracts;
using HeartRate.Api.Security;
using HeartRate.Domain.Entities;
using HeartRate.Infrastructure.MongoDb;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HeartRate.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/users/me").WithTags("Users").RequireAuthorization();
        group.MapGet("/", GetProfile).Produces<UserResponse>();
        group.MapPatch("/", UpdateProfile).Produces<UserResponse>();
        group.MapPost("/change-password", ChangePassword);
        group.MapDelete("/", DeleteAccount);
        return routes;
    }

    private static async Task<IResult> GetProfile(ClaimsPrincipal principal, HeartRateDbContext db, CancellationToken ct)
    {
        var userId = principal.UserId();
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId, ct);
        return Results.Ok(UserResponse.From(user));
    }

    private static async Task<IResult> UpdateProfile(
        UpdateProfileRequest request, ClaimsPrincipal principal, HeartRateDbContext db, CancellationToken ct)
    {
        if ((request.Name is not null && !Validation.Name(request.Name))
            || request.Surname?.Length > 100
            || (request.TimeZone is not null && !Validation.TimeZone(request.TimeZone))
            || request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            return Results.BadRequest(new { message = "Niepoprawne dane profilu lub strefa czasowa." });

        var userId = principal.UserId();
        var user = await db.Users.SingleAsync(x => x.Id == userId, ct);
        if (request.Name is not null) user.Name = request.Name.Trim();
        if (request.Surname is not null) user.Surname = request.Surname.Trim();
        if (request.DateOfBirth is not null) user.DateOfBirth = request.DateOfBirth;
        if (request.TimeZone is not null) user.TimeZone = request.TimeZone;
        await db.SaveChangesAsync(ct);
        return Results.Ok(UserResponse.From(user));
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request, ClaimsPrincipal principal, HeartRateDbContext db,
        IPasswordHasher<User> hasher, CancellationToken ct)
    {
        if (!Validation.Password(request.NewPassword) || request.CurrentPassword is not { Length: <= 128 })
            return Results.BadRequest(new { message = "Nowe hasło musi mieć 8–128 znaków. Podaj obecne hasło." });

        var userId = principal.UserId();
        var user = await db.Users.SingleAsync(x => x.Id == userId, ct);
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword)
            == PasswordVerificationResult.Failed)
            return Results.BadRequest(new { message = "Obecne hasło jest niepoprawne." });

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword!);
        user.TokenVersion++;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAccount(ClaimsPrincipal principal, HeartRateDbContext db, CancellationToken ct)
    {
        var userId = principal.UserId();
        var user = await db.Users.SingleAsync(x => x.Id == userId, ct);
        user.IsActive = false;
        user.TokenVersion++;
        await db.SaveChangesAsync(ct);

        await DataDeletion.DeleteAsync(db, db.Measurements.Where(x => x.UserId == userId), ct);
        await DataDeletion.DeleteAsync(db, db.Sessions.Where(x => x.UserId == userId), ct);
        await DataDeletion.DeleteAsync(db, db.RefreshSessions.Where(x => x.UserId == userId), ct);
        var devices = await db.Devices.Where(x => x.UserId == userId).ToListAsync(ct);
        foreach (var device in devices)
        {
            device.UserId = null;
            device.PairingCodeHash = null;
            device.PairingCodeExpiresAtUtc = null;
        }
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
