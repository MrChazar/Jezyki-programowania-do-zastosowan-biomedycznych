using System.Security.Claims;
using HeartRate.Api.Contracts;
using HeartRate.Api.Security;
using HeartRate.Infrastructure.MongoDb;
using Microsoft.EntityFrameworkCore;

namespace HeartRate.Api.Endpoints;

public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/devices").WithTags("Devices").RequireAuthorization();
        group.MapGet("/", GetDevices).Produces<PagedResponse<DeviceResponse>>();
        group.MapGet("/{id}", GetDevice).Produces<DeviceResponse>();
        group.MapPost("/pair", Pair).Produces<DeviceResponse>().RequireRateLimiting("pair");
        group.MapDelete("/{id}/pairing", Unpair);
        return routes;
    }

    private static async Task<IResult> GetDevices(
        [AsParameters] Pagination pagination, ClaimsPrincipal user, HeartRateDbContext db, CancellationToken ct)
    {
        if (!pagination.IsValid()) return Results.BadRequest(new { message = "strona >= 1, rozmiar strony od 1 do 100." });
        var userId = user.UserId();
        var query = db.Devices.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.Id);
        return Results.Ok(await pagination.ReadAsync(query, DeviceResponse.From, ct));
    }

    private static async Task<IResult> GetDevice(string id, ClaimsPrincipal user, HeartRateDbContext db, CancellationToken ct)
    {
        var userId = user.UserId();
        var device = await db.Devices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        return device is null ? Results.NotFound() : Results.Ok(DeviceResponse.From(device));
    }

    private static async Task<IResult> Pair(
        PairDeviceRequest request, ClaimsPrincipal user, HeartRateDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId) || request.DeviceId.Length > 100
            || request.PairingCode is not { Length: >= 16 and <= 128 })
            return Results.BadRequest(new { message = "Podaj identyfikator i kod parowania urządzenia." });

        var hash = TokenService.Hash(request.PairingCode);
        var now = DateTime.UtcNow;
        var device = await db.Devices.SingleOrDefaultAsync(x => x.Id == request.DeviceId
            && x.UserId == null && x.IsActive && x.PairingCodeHash == hash && x.PairingCodeExpiresAtUtc > now, ct);
        if (device is null)
            return Results.BadRequest(new { message = "Nieprawidłowy lub wygasły kod, albo urządzenie jest już przypisane." });

        device.UserId = user.UserId();
        device.PairingCodeHash = null;
        device.PairingCodeExpiresAtUtc = null;
        await db.SaveChangesAsync(ct);
        return Results.Ok(DeviceResponse.From(device));
    }

    private static async Task<IResult> Unpair(string id, ClaimsPrincipal user, HeartRateDbContext db, CancellationToken ct)
    {
        var userId = user.UserId();
        var device = await db.Devices.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (device is null) return Results.NotFound();

        device.UserId = null;
        device.PairingCodeHash = null;
        device.PairingCodeExpiresAtUtc = null;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
