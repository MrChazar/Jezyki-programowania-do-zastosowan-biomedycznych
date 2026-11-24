using System.Security.Claims;
using HeartRate.Api.Contracts;
using HeartRate.Api.Security;
using HeartRate.Application.Measurements;
using HeartRate.Infrastructure.MongoDb;
using Microsoft.EntityFrameworkCore;

namespace HeartRate.Api.Endpoints;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/sessions").WithTags("Sessions").RequireAuthorization();
        group.MapGet("/", GetSessions).Produces<PagedResponse<SessionResponse>>();
        group.MapGet("/{id}", GetSession).Produces<SessionResponse>();
        group.MapGet("/{id}/measurements", GetMeasurements).Produces<PagedResponse<MeasurementResponse>>();
        group.MapGet("/{id}/statistics", GetStatistics).Produces<HeartRateStatistics>();
        group.MapPatch("/{id}", UpdateSession).Produces<SessionResponse>();
        group.MapDelete("/{id}", DeleteSession);
        return routes;
    }

    private static async Task<IResult> GetSessions(
        [AsParameters] Pagination pagination, ClaimsPrincipal user, HeartRateDbContext db, CancellationToken ct)
    {
        if (!pagination.IsValid()) return Results.BadRequest(new { message = "strona >= 1, rozmiar strony od 1 do 100." });
        var userId = user.UserId();
        var query = db.Sessions.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.StartedAtUtc).ThenBy(x => x.Id);
        return Results.Ok(await pagination.ReadAsync(query, SessionResponse.From, ct));
    }

    private static async Task<IResult> GetSession(string id, ClaimsPrincipal user, HeartRateDbContext db, CancellationToken ct)
    {
        var userId = user.UserId();
        var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        return session is null ? Results.NotFound() : Results.Ok(SessionResponse.From(session));
    }

    private static async Task<IResult> GetMeasurements(
        string id, [AsParameters] Pagination pagination, ClaimsPrincipal user, HeartRateDbContext db, CancellationToken ct)
    {
        if (!pagination.IsValid()) return Results.BadRequest(new { message = "page >= 1, pageSize od 1 do 100." });
        var userId = user.UserId();
        if (!await db.Sessions.AnyAsync(x => x.Id == id && x.UserId == userId, ct)) return Results.NotFound();

        var query = db.Measurements.AsNoTracking().Where(x => x.SessionId == id && x.UserId == userId)
            .OrderBy(x => x.MeasuredAtUtc).ThenBy(x => x.Id);
        return Results.Ok(await pagination.ReadAsync(query, MeasurementResponse.From, ct));
    }

    private static async Task<IResult> GetStatistics(string id, ClaimsPrincipal user, HeartRateDbContext db, CancellationToken ct)
    {
        var userId = user.UserId();
        if (!await db.Sessions.AnyAsync(x => x.Id == id && x.UserId == userId, ct)) return Results.NotFound();
        var items = await db.Measurements.AsNoTracking().Where(x => x.SessionId == id && x.UserId == userId)
            .OrderBy(x => x.MeasuredAtUtc).ThenBy(x => x.Id).ToListAsync(ct);
        return Results.Ok(HeartRateStatistics.Calculate(items));
    }

    private static async Task<IResult> UpdateSession(
        string id, UpdateSessionRequest request, ClaimsPrincipal user, HeartRateDbContext db, CancellationToken ct)
    {
        if ((request.Name is not null && !Validation.Name(request.Name))
            || request.Tags is { Count: > 10 }
            || (request.Tags?.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 30) ?? false))
            return Results.BadRequest(new { message = "Nazwa: 1–100 znaków. Maksymalnie 10 tagów po 1–30 znaków." });

        var userId = user.UserId();
        var session = await db.Sessions.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (session is null) return Results.NotFound();

        if (request.Name is not null) session.Name = request.Name.Trim();
        if (request.Tags is not null)
            session.Tags = request.Tags.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        await db.SaveChangesAsync(ct);
        return Results.Ok(SessionResponse.From(session));
    }

    private static async Task<IResult> DeleteSession(string id, ClaimsPrincipal user, HeartRateDbContext db, CancellationToken ct)
    {
        var userId = user.UserId();
        var session = await db.Sessions.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (session is null) return Results.NotFound();

        await DataDeletion.DeleteAsync(db, db.Measurements.Where(x => x.SessionId == id && x.UserId == userId), ct);
        db.Sessions.Remove(session);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
