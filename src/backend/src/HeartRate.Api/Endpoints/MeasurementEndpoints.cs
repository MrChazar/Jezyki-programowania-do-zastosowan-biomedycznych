using System.Security.Claims;
using HeartRate.Api.Contracts;
using HeartRate.Api.Security;
using HeartRate.Application.Measurements;
using HeartRate.Infrastructure.MongoDb;
using Microsoft.EntityFrameworkCore;

namespace HeartRate.Api.Endpoints;

public static class MeasurementEndpoints
{
    public static IEndpointRouteBuilder MapMeasurementEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/measurements").WithTags("Measurements").RequireAuthorization();
        group.MapGet("/", GetIds).Produces<PagedResponse<MeasurementIdResponse>>();
        group.MapGet("/statistics", GetMonthlyStatistics).Produces<MonthlyStatisticsResponse>();
        group.MapGet("/dates", GetDates).Produces<PagedResponse<MeasurementDateResponse>>();
        group.MapGet("/day/{date}", GetDay).Produces<PagedResponse<MeasurementResponse>>();
        group.MapGet("/{id}", GetMeasurement).Produces<MeasurementResponse>();
        group.MapGet("/{id}/statistics", GetMeasurementStatistics).Produces<HeartRateStatistics>();
        group.MapDelete("/{id}", DeleteMeasurement);
        return routes;
    }

    private static async Task<IResult> GetIds(
        [AsParameters] Pagination pagination, ClaimsPrincipal user, HeartRateDbContext db, CancellationToken ct,
        string? sessionId = null)
    {
        if (!pagination.IsValid()) return Results.BadRequest(new { message = "page >= 1, pageSize od 1 do 100." });
        var userId = user.UserId();
        var query = db.Measurements.AsNoTracking().Where(x => x.UserId == userId);
        if (sessionId is not null) query = query.Where(x => x.SessionId == sessionId);
        return Results.Ok(await pagination.ReadAsync(
            query.OrderByDescending(x => x.MeasuredAtUtc).ThenBy(x => x.Id), MeasurementIdResponse.From, ct));
    }

    private static async Task<IResult> GetMonthlyStatistics(ClaimsPrincipal user, HeartRateDbContext db, CancellationToken ct)
    {
        var userId = user.UserId();
        var to = DateTime.UtcNow;
        var from = to.AddMonths(-1);
        var items = await db.Measurements.AsNoTracking()
            .Where(x => x.UserId == userId && x.MeasuredAtUtc >= from && x.MeasuredAtUtc <= to)
            .OrderBy(x => x.MeasuredAtUtc).ThenBy(x => x.Id).ToListAsync(ct);
        return Results.Ok(new MonthlyStatisticsResponse(from, to, HeartRateStatistics.Calculate(items)));
    }

    private static async Task<IResult> GetDates(
        [AsParameters] Pagination pagination, ClaimsPrincipal principal, HeartRateDbContext db,
        CancellationToken ct, DateOnly? month = null)
    {
        if (!pagination.IsValid() || month?.Year is < 1900 or > 9998)
            return Results.BadRequest(new { message = "Podaj poprawny miesiąc i paginację (strona >= 1, rozmiar strony: 1–100)." });

        var userId = principal.UserId();
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId, ct);
        var date = month ?? MeasurementTime.LocalDate(DateTime.UtcNow, user.TimeZone);
        var first = new DateOnly(date.Year, date.Month, 1);
        var from = MeasurementTime.DayUtcRange(first, user.TimeZone).Start;
        var to = MeasurementTime.DayUtcRange(first.AddMonths(1), user.TimeZone).Start;
        var measurements = await db.Measurements.AsNoTracking()
            .Where(x => x.UserId == userId && x.MeasuredAtUtc >= from && x.MeasuredAtUtc < to)
            .OrderByDescending(x => x.MeasuredAtUtc).ThenBy(x => x.Id).ToListAsync(ct);

        // Grupowanie w pamięci: provider MongoDB EF nie obsługuje GroupBy.
        var days = measurements.GroupBy(x => MeasurementTime.LocalDate(x.MeasuredAtUtc, user.TimeZone))
            .Select(x => new MeasurementDateResponse(x.Key, x.Select(m => m.Id).ToList())).ToList();
        return Results.Ok(new PagedResponse<MeasurementDateResponse>(
            days.Skip((pagination.Page - 1) * pagination.PageSize).Take(pagination.PageSize).ToList(),
            pagination.Page, pagination.PageSize, days.Count));
    }

    private static async Task<IResult> GetDay(
        DateOnly date, [AsParameters] Pagination pagination, ClaimsPrincipal principal,
        HeartRateDbContext db, CancellationToken ct)
    {
        if (!pagination.IsValid() || date.Year is < 1900 or > 9998)
            return Results.BadRequest(new { message = "Podaj poprawny dzień i paginację (strona >= 1, rozmiar strony: 1–100)." });

        var userId = principal.UserId();
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId, ct);
        var (from, to) = MeasurementTime.DayUtcRange(date, user.TimeZone);
        var query = db.Measurements.AsNoTracking()
            .Where(x => x.UserId == userId && x.MeasuredAtUtc >= from && x.MeasuredAtUtc < to)
            .OrderBy(x => x.MeasuredAtUtc).ThenBy(x => x.Id);
        return Results.Ok(await pagination.ReadAsync(query, MeasurementResponse.From, ct));
    }

    private static async Task<IResult> GetMeasurement(
        string id, ClaimsPrincipal user, HeartRateDbContext db, CancellationToken ct)
    {
        var userId = user.UserId();
        var item = await db.Measurements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        return item is null ? Results.NotFound() : Results.Ok(MeasurementResponse.From(item));
    }

    private static async Task<IResult> GetMeasurementStatistics(
        string id, ClaimsPrincipal user, HeartRateDbContext db, CancellationToken ct)
    {
        var userId = user.UserId();
        var item = await db.Measurements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        return item is null ? Results.NotFound() : Results.Ok(HeartRateStatistics.Calculate([item]));
    }

    private static async Task<IResult> DeleteMeasurement(
        string id, ClaimsPrincipal user, HeartRateDbContext db, CancellationToken ct)
    {
        var userId = user.UserId();
        var item = await db.Measurements.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (item is null) return Results.NotFound();
        db.Measurements.Remove(item);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
