using System.Net;
using System.Net.Http.Json;
using HeartRate.Api.Contracts;
using HeartRate.Api.Security;
using HeartRate.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HeartRate.Api.Tests.Endpoints;

public sealed class DeviceTests
{
    private const string PairingCode = "1234567890abcdef";

    [Fact]
    public async Task GetDevices_ReturnsCurrentUsersDevices()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/devices/", app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await app.ReadAsync<PagedResponse<DeviceResponse>>(response);
        Assert.Equal("seed-device", Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task GetDevices_RejectsInvalidPageSize()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/devices/?pageSize=0", app.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDevices_HidesOtherUsersDevices()
    {
        await using var app = new EndpointTestContext();
        var user = await app.RegisterAsync();
        await app.LoginAsync(user.Email, "Haslo123!");
        using var response = await app.Client.GetAsync("/api/devices/", app.CancellationToken);
        var page = await app.ReadAsync<PagedResponse<DeviceResponse>>(response);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task GetDevice_ReturnsOwnedDevice()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/devices/seed-device", app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("seed-device", (await app.ReadAsync<DeviceResponse>(response)).Id);
    }

    [Fact]
    public async Task GetDevice_ReturnsNotFoundForUnknownDevice()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/devices/missing", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDevice_HidesOtherUsersDevice()
    {
        await using var app = new EndpointTestContext();
        var user = await app.RegisterAsync();
        await app.LoginAsync(user.Email, "Haslo123!");
        using var response = await app.Client.GetAsync("/api/devices/seed-device", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Pair_AssignsDeviceAndConsumesPairingCode()
    {
        await using var app = new EndpointTestContext();
        await AddPairableDeviceAsync(app);
        await app.LoginAsync();

        using var response = await app.Client.PostAsJsonAsync("/api/devices/pair",
            new PairDeviceRequest("pairable-device", PairingCode), app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("pairable-device", (await app.ReadAsync<DeviceResponse>(response)).Id);
        await app.Factory.InDatabaseAsync(async db =>
        {
            var device = await db.Devices.SingleAsync(x => x.Id == "pairable-device", app.CancellationToken);
            Assert.Equal(EndpointTestContext.SeedUserId, device.UserId);
            Assert.Null(device.PairingCodeHash);
        });
    }

    [Fact]
    public async Task Pair_RejectsMalformedCode()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.PostAsJsonAsync("/api/devices/pair",
            new PairDeviceRequest("seed-device", "short"), app.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Pair_RejectsExpiredCode()
    {
        await using var app = new EndpointTestContext();
        await AddPairableDeviceAsync(app, DateTime.UtcNow.AddMinutes(-1));
        await app.LoginAsync();
        using var response = await app.Client.PostAsJsonAsync("/api/devices/pair",
            new PairDeviceRequest("pairable-device", PairingCode), app.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Pair_RejectsAlreadyPairedDevice()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.PostAsJsonAsync("/api/devices/pair",
            new PairDeviceRequest("seed-device", PairingCode), app.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unpair_RemovesDeviceOwnership()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.DeleteAsync("/api/devices/seed-device/pairing", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var get = await app.Client.GetAsync("/api/devices/seed-device", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        await app.Factory.InDatabaseAsync(async db =>
            Assert.Null((await db.Devices.SingleAsync(x => x.Id == "seed-device", app.CancellationToken)).UserId));
    }

    [Fact]
    public async Task Unpair_ReturnsNotFoundForUnknownDevice()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.DeleteAsync("/api/devices/missing/pairing", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unpair_CannotRemoveOtherUsersDevice()
    {
        await using var app = new EndpointTestContext();
        var user = await app.RegisterAsync();
        await app.LoginAsync(user.Email, "Haslo123!");
        using var response = await app.Client.DeleteAsync("/api/devices/seed-device/pairing", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Task AddPairableDeviceAsync(EndpointTestContext app, DateTime? expiresAtUtc = null) =>
        app.Factory.InDatabaseAsync(async db =>
        {
            db.Devices.Add(new Device
            {
                Id = "pairable-device", Name = "New sensor", PairingCodeHash = TokenService.Hash(PairingCode),
                PairingCodeExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddMinutes(5)
            });
            await db.SaveChangesAsync(app.CancellationToken);
        });
}
