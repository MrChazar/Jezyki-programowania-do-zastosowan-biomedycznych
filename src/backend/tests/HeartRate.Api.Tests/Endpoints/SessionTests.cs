using System.Net;
using HeartRate.Api.Contracts;
using HeartRate.Application.Measurements;
using HeartRate.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HeartRate.Api.Tests.Endpoints;

public sealed class SessionTests
{
    [Fact]
    public async Task GetSessions_ReturnsPagedCurrentUsersSessions()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/sessions/?page=1&pageSize=1", app.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await app.ReadAsync<PagedResponse<SessionResponse>>(response);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal("seed-session", Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task GetSessions_RejectsInvalidPagination()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/sessions/?page=0", app.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetSessions_ReturnsEmptyPageForOtherUser()
    {
        await using var app = new EndpointTestContext();
        var user = await app.RegisterAsync();
        await app.LoginAsync(user.Email, "Haslo123!");
        using var response = await app.Client.GetAsync("/api/sessions/", app.CancellationToken);
        var page = await app.ReadAsync<PagedResponse<SessionResponse>>(response);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task GetSession_ReturnsSession()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/sessions/seed-session", app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await app.ReadAsync<SessionResponse>(response);
        Assert.Equal("seed-session", session.Id);
        Assert.Equal("seed-device", session.DeviceId);
    }

    [Fact]
    public async Task GetSession_ReturnsNotFoundForMissingSession()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/sessions/missing", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSession_HidesOtherUsersSession()
    {
        await using var app = new EndpointTestContext();
        var user = await app.RegisterAsync();
        await app.LoginAsync(user.Email, "Haslo123!");
        using var response = await app.Client.GetAsync("/api/sessions/seed-session", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMeasurements_ReturnsOrderedPageOfSessionSamples()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync(
            "/api/sessions/seed-session/measurements?page=1&pageSize=2", app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await app.ReadAsync<PagedResponse<MeasurementResponse>>(response);
        Assert.Equal(60, page.TotalCount);
        Assert.Equal(new[] { "seed-session-01", "seed-session-02" }, page.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task GetMeasurements_RejectsInvalidPagination()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync(
            "/api/sessions/seed-session/measurements?pageSize=101", app.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMeasurements_HidesOtherUsersSession()
    {
        await using var app = new EndpointTestContext();
        var user = await app.RegisterAsync();
        await app.LoginAsync(user.Email, "Haslo123!");
        using var response = await app.Client.GetAsync(
            "/api/sessions/seed-session/measurements", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStatistics_CalculatesSessionStatistics()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/sessions/seed-session/statistics", app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var statistics = await app.ReadAsync<HeartRateStatistics>(response);
        Assert.Equal(60, statistics.MeasurementCount);
        Assert.Equal(80f, statistics.MinimumBpm);
        Assert.Equal(99f, statistics.MaximumBpm);
        Assert.Equal(89.5, statistics.AverageBpm);
    }

    [Fact]
    public async Task GetStatistics_ReturnsNotFoundForMissingSession()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/sessions/missing/statistics", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStatistics_ReturnsEmptyStatisticsForSessionWithoutMeasurements()
    {
        await using var app = new EndpointTestContext();
        await app.Factory.InDatabaseAsync(async db =>
        {
            db.Sessions.Add(new Session { Id = "empty-session", UserId = EndpointTestContext.SeedUserId,
                Name = "Empty", StartedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync(app.CancellationToken);
        });
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/sessions/empty-session/statistics", app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var statistics = await app.ReadAsync<HeartRateStatistics>(response);
        Assert.Equal(0, statistics.MeasurementCount);
        Assert.Null(statistics.AverageBpm);
    }

    [Fact]
    public async Task UpdateSession_ChangesNameAndTags()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.PatchAsync("/api/sessions/seed-session",
            new UpdateSessionRequest("Morning run", ["cardio", "outdoor"]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await app.ReadAsync<SessionResponse>(response);
        Assert.Equal("Morning run", session.Name);
        Assert.Equal(new[] { "cardio", "outdoor" }, session.Tags);
    }

    [Fact]
    public async Task UpdateSession_RejectsTooManyTags()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.PatchAsync("/api/sessions/seed-session",
            new UpdateSessionRequest(Tags: Enumerable.Range(0, 11).Select(x => $"tag{x}").ToList()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSession_TrimsAndDeduplicatesTagsIgnoringCase()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.PatchAsync("/api/sessions/seed-session",
            new UpdateSessionRequest(Tags: [" Cardio ", "cardio", "REST"]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await app.ReadAsync<SessionResponse>(response);
        Assert.Equal(new[] { "Cardio", "REST" }, session.Tags);
    }

    [Fact]
    public async Task UpdateSession_DoesNotChangeOtherUsersSession()
    {
        await using var app = new EndpointTestContext();
        var user = await app.RegisterAsync();
        await app.LoginAsync(user.Email, "Haslo123!");
        using var response = await app.PatchAsync("/api/sessions/seed-session", new UpdateSessionRequest("Changed"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSession_RemovesSessionAndItsMeasurements()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.DeleteAsync("/api/sessions/seed-session", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await app.Factory.InDatabaseAsync(async db =>
        {
            Assert.False(await db.Sessions.AnyAsync(x => x.Id == "seed-session", app.CancellationToken));
            Assert.False(await db.Measurements.AnyAsync(x => x.SessionId == "seed-session", app.CancellationToken));
            Assert.True(await db.Measurements.AnyAsync(x => x.Id == "seed-normal-1", app.CancellationToken));
        });
    }

    [Fact]
    public async Task DeleteSession_ReturnsNotFoundForMissingSession()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.DeleteAsync("/api/sessions/missing", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSession_CannotDeleteOtherUsersSession()
    {
        await using var app = new EndpointTestContext();
        var user = await app.RegisterAsync();
        await app.LoginAsync(user.Email, "Haslo123!");
        using var response = await app.Client.DeleteAsync("/api/sessions/seed-session", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
