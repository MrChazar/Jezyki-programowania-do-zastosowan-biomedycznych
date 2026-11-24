using System.Security.Claims;
using HeartRate.Api.Contracts;
using HeartRate.Api.Security;
using HeartRate.Domain.Entities;
using HeartRate.Infrastructure.MongoDb;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HeartRate.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Auth");
        group.MapPost("/register", Register).Produces<UserResponse>(201).AllowAnonymous().RequireRateLimiting("auth");
        group.MapPost("/login", Login).Produces<TokenResponse>().AllowAnonymous().RequireRateLimiting("auth");
        group.MapPost("/refresh", Refresh).Produces<TokenResponse>().AllowAnonymous().RequireRateLimiting("auth");
        group.MapPost("/logout", Logout).RequireAuthorization();
        group.MapPost("/logout-all", LogoutAll).RequireAuthorization();
        return routes;
    }

    private static async Task<IResult> Register(
        RegisterRequest request, HeartRateDbContext db, IPasswordHasher<User> hasher, CancellationToken ct)
    {


        if (!Validation.Name(request.Name) || !Validation.Email(request.Email)
            || !Validation.Password(request.Password) || request.Surname?.Length > 100)
            return Results.BadRequest(new { message = "Podaj imię, poprawny email i hasło (8–128 znaków)." });

        var email = request.Email!.Trim();
        var normalized = email.ToUpperInvariant();
        if (await db.Users.AnyAsync(x => x.EmailNormalized == normalized, ct))
            return Results.Conflict(new { message = "Ten email jest już zajęty." });

        var user = new User
        {
            Name = request.Name!.Trim(), Surname = request.Surname?.Trim() ?? "",
            Email = email, EmailNormalized = normalized, PasswordHash = ""
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password!);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return Results.Created("/api/users/me", UserResponse.From(user));
    }

    private static async Task<IResult> Login(
        LoginRequest request, HeartRateDbContext db, IPasswordHasher<User> hasher,
        TokenService tokens, IOptions<JwtOptions> options, CancellationToken ct)
    {
        if (!Validation.Email(request.Email) || !Validation.Password(request.Password))
            return Results.Unauthorized();

        var email = request.Email!.Trim().ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.EmailNormalized == email && x.IsActive, ct);
        
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password!)
            == PasswordVerificationResult.Failed)
            return Results.Unauthorized();

        var refreshToken = TokenService.NewRefreshToken();
        var session = new RefreshSession
        {
            UserId = user.Id, TokenHash = TokenService.Hash(refreshToken),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(options.Value.RefreshTokenDays),
            TokenVersion = user.TokenVersion
        };
        db.RefreshSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return Results.Ok(tokens.Issue(user, session, refreshToken));
    }

    private static async Task<IResult> Refresh(
        RefreshRequest request, HeartRateDbContext db, TokenService tokens, CancellationToken ct)
    {
        if (request.RefreshToken is not { Length: 64 })
            return Results.Unauthorized();

        var hash = TokenService.Hash(request.RefreshToken);
        var now = DateTime.UtcNow;
        var session = await db.RefreshSessions.SingleOrDefaultAsync(
            x => x.TokenHash == hash && x.RevokedAtUtc == null && x.ExpiresAtUtc > now, ct);
        if (session is null)
            return Results.Unauthorized();

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == session.UserId && x.IsActive, ct);
        if (user is null || user.TokenVersion != session.TokenVersion)
            return Results.Unauthorized();

        var refreshToken = TokenService.NewRefreshToken();
        session.TokenHash = TokenService.Hash(refreshToken);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Results.Unauthorized(); }

        return Results.Ok(tokens.Issue(user, session, refreshToken));
    }

    private static async Task<IResult> Logout(ClaimsPrincipal user, HeartRateDbContext db, CancellationToken ct)
    {
        var userId = user.UserId();
        var sessionId = user.RefreshSessionId();
        var session = await db.RefreshSessions.SingleAsync(x => x.Id == sessionId && x.UserId == userId, ct);
        session.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAll(ClaimsPrincipal principal, HeartRateDbContext db, CancellationToken ct)
    {
        var userId = principal.UserId();
        var user = await db.Users.SingleAsync(x => x.Id == userId, ct);
        user.TokenVersion++;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
