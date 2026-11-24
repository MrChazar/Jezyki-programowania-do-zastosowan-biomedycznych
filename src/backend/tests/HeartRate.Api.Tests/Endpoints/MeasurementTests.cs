using System.Net;
using HeartRate.Api.Contracts;
using HeartRate.Application.Measurements;
using HeartRate.Domain.Entities;

namespace HeartRate.Api.Tests.Endpoints;

public sealed class MeasurementTests
{
    [Fact]
    public async Task GetIds_ReturnsPagedMeasurementIds()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/measurements/?pageSize=2", app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await app.ReadAsync<PagedResponse<MeasurementIdResponse>>(response);
        Assert.Equal(63, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task GetIds_RejectsInvalidPagination()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/measurements/?page=-1", app.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetIds_CanFilterBySessionAndHidesOtherUsersMeasurements()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var filtered = await app.Client.GetAsync(
            "/api/measurements/?sessionId=seed-session", app.CancellationToken);
        var page = await app.ReadAsync<PagedResponse<MeasurementIdResponse>>(filtered);
        Assert.Equal(60, page.TotalCount);
        Assert.All(page.Items, item => Assert.Equal("seed-session", item.SessionId));

        var user = await app.RegisterAsync();
        await app.LoginAsync(user.Email, "Haslo123!");
        using var other = await app.Client.GetAsync("/api/measurements/", app.CancellationToken);
        Assert.Equal(0, (await app.ReadAsync<PagedResponse<MeasurementIdResponse>>(other)).TotalCount);
    }

    [Fact]
    public async Task GetMonthlyStatistics_ReturnsRecentMeasurements()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/measurements/statistics", app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await app.ReadAsync<MonthlyStatisticsResponse>(response);
        Assert.Equal(63, result.Statistics.MeasurementCount);
        Assert.Equal(72f, result.Statistics.MinimumBpm);
        Assert.Equal(99f, result.Statistics.MaximumBpm);
        Assert.True(result.FromUtc < result.ToUtc);
    }

    [Fact]
    public async Task GetMonthlyStatistics_ReturnsEmptyForUserWithoutMeasurements()
    {
        await using var app = new EndpointTestContext();
        var user = await app.RegisterAsync();
        await app.LoginAsync(user.Email, "Haslo123!");
        using var response = await app.Client.GetAsync("/api/measurements/statistics", app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await app.ReadAsync<MonthlyStatisticsResponse>(response);
        Assert.Equal(0, result.Statistics.MeasurementCount);
        Assert.Null(result.Statistics.AverageBpm);
    }

    [Fact]
    public async Task GetMonthlyStatistics_ExcludesMeasurementOlderThanOneMonth()
    {
        await using var app = new EndpointTestContext();
        await app.Factory.InDatabaseAsync(async db =>
        {
            db.Measurements.Add(Measurement.Create("old", EndpointTestContext.SeedUserId, null,
                DateTime.UtcNow.AddMonths(-1).AddDays(-1), 200));
            await db.SaveChangesAsync(app.CancellationToken);
        });
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/measurements/statistics", app.CancellationToken);
        var result = await app.ReadAsync<MonthlyStatisticsResponse>(response);
        Assert.Equal(63, result.Statistics.MeasurementCount);
        Assert.Equal(99f, result.Statistics.MaximumBpm);
    }

    [Fact]
    public async Task GetDates_GroupsMeasurementsByUsersLocalDay()
    {
        await using var app = new EndpointTestContext();
        await AddMidnightMeasurementAsync(app);
        await app.LoginAsync();
        using var response = await app.Client.GetAsync(
            "/api/measurements/dates?month=2026-01-01", app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await app.ReadAsync<PagedResponse<MeasurementDateResponse>>(response);
        var day = Assert.Single(page.Items);
        Assert.Equal(new DateOnly(2026, 1, 16), day.Date);
        Assert.Equal("midnight-measurement", Assert.Single(day.MeasurementIds));
    }

    [Theory]
    [InlineData("month=1899-01-01")]
    [InlineData("pageSize=101")]
    public async Task GetDates_RejectsInvalidMonthOrPagination(string query)
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync($"/api/measurements/dates?{query}", app.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDates_ReturnsEmptyPageForMonthWithoutMeasurements()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync(
            "/api/measurements/dates?month=2000-01-01", app.CancellationToken);
        var page = await app.ReadAsync<PagedResponse<MeasurementDateResponse>>(response);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task GetDay_UsesLocalDateAcrossUtcMidnight()
    {
        await using var app = new EndpointTestContext();
        await AddMidnightMeasurementAsync(app);
        await app.LoginAsync();
        using var response = await app.Client.GetAsync(
            "/api/measurements/day/2026-01-16", app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await app.ReadAsync<PagedResponse<MeasurementResponse>>(response);
        Assert.Equal("midnight-measurement", Assert.Single(page.Items).Id);
    }

    [Theory]
    [InlineData("1899-01-01")]
    [InlineData("2026-01-16?page=0")]
    public async Task GetDay_RejectsInvalidDateOrPagination(string dateAndQuery)
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync(
            $"/api/measurements/day/{dateAndQuery}", app.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDay_ReturnsEmptyForDayWithoutMeasurements()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/measurements/day/2000-01-01", app.CancellationToken);
        var page = await app.ReadAsync<PagedResponse<MeasurementResponse>>(response);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task GetMeasurement_ReturnsOwnedMeasurement()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/measurements/seed-normal-1", app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var measurement = await app.ReadAsync<MeasurementResponse>(response);
        Assert.Equal(72f, measurement.Bpm);
        Assert.Equal(10, measurement.IntervalSeconds);
    }

    [Fact]
    public async Task GetMeasurement_ReturnsNotFoundForUnknownId()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/measurements/missing", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMeasurement_HidesOtherUsersMeasurement()
    {
        await using var app = new EndpointTestContext();
        var user = await app.RegisterAsync();
        await app.LoginAsync(user.Email, "Haslo123!");
        using var response = await app.Client.GetAsync("/api/measurements/seed-normal-1", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMeasurementStatistics_ReturnsSingleSampleStatistics()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync(
            "/api/measurements/seed-normal-1/statistics", app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var statistics = await app.ReadAsync<HeartRateStatistics>(response);
        Assert.Equal(1, statistics.MeasurementCount);
        Assert.Equal(72, statistics.AverageBpm);
    }

    [Fact]
    public async Task GetMeasurementStatistics_ReturnsNotFoundForUnknownId()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.GetAsync("/api/measurements/missing/statistics", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMeasurementStatistics_HidesOtherUsersMeasurement()
    {
        await using var app = new EndpointTestContext();
        var user = await app.RegisterAsync();
        await app.LoginAsync(user.Email, "Haslo123!");
        using var response = await app.Client.GetAsync(
            "/api/measurements/seed-normal-1/statistics", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMeasurement_RemovesOnlySelectedMeasurement()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.DeleteAsync("/api/measurements/seed-normal-1", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var missing = await app.Client.GetAsync("/api/measurements/seed-normal-1", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var remaining = await app.Client.GetAsync("/api/measurements/seed-normal-2", app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, remaining.StatusCode);
    }

    [Fact]
    public async Task DeleteMeasurement_ReturnsNotFoundForUnknownId()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.DeleteAsync("/api/measurements/missing", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMeasurement_CannotDeleteOtherUsersMeasurement()
    {
        await using var app = new EndpointTestContext();
        var user = await app.RegisterAsync();
        await app.LoginAsync(user.Email, "Haslo123!");
        using var response = await app.Client.DeleteAsync("/api/measurements/seed-normal-1", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Task AddMidnightMeasurementAsync(EndpointTestContext app) =>
        app.Factory.InDatabaseAsync(async db =>
        {
            db.Measurements.Add(Measurement.Create("midnight-measurement", EndpointTestContext.SeedUserId,
                null, new DateTime(2026, 1, 15, 23, 30, 0, DateTimeKind.Utc), 70));
            await db.SaveChangesAsync(app.CancellationToken);
        });
}
