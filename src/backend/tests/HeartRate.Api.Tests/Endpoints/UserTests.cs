using System.Net;
using System.Net.Http.Json;
using HeartRate.Api.Contracts;
using Microsoft.EntityFrameworkCore;

namespace HeartRate.Api.Tests.Endpoints;

public sealed class UserTests
{
    [Fact]
    public async Task GetProfile_ReturnsCurrentUserWithoutSensitiveFields()
    {
        await using var app = new EndpointTestContext();
        var created = await app.RegisterAsync(email: "profile@example.com");
        await app.LoginAsync(created.Email, "Haslo123!");

        using var response = await app.Client.GetAsync("/api/users/me/", app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await app.ReadAsync<UserResponse>(response);
        Assert.Equal(created.Id, profile.Id);
        Assert.Equal(created.Email, profile.Email);
        var json = await response.Content.ReadAsStringAsync(app.CancellationToken);
        Assert.DoesNotContain("passwordHash", json);
        Assert.DoesNotContain("tokenHash", json);
    }

    [Fact]
    public async Task GetProfile_RequiresAuthentication()
    {
        await using var app = new EndpointTestContext();
        using var response = await app.Client.GetAsync("/api/users/me/", app.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ChangesRequestedFields()
    {
        await using var app = new EndpointTestContext();
        var created = await app.RegisterAsync();
        await app.LoginAsync(created.Email, "Haslo123!");
        var birthDate = new DateOnly(1995, 5, 16);

        using var response = await app.PatchAsync("/api/users/me/",
            new UpdateProfileRequest("Maria", "Kowalska", birthDate, "Europe/Warsaw"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await app.ReadAsync<UserResponse>(response);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Maria", updated.Name);
        Assert.Equal("Kowalska", updated.Surname);
        Assert.Equal(birthDate, updated.DateOfBirth);
        Assert.Equal(created.Email, updated.Email);
    }

    [Theory]
    [InlineData(" ", "Europe/Warsaw")]
    [InlineData("Anna", "Invalid/Zone")]
    public async Task UpdateProfile_RejectsInvalidNameOrTimeZone(string name, string zone)
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.PatchAsync("/api/users/me/", new UpdateProfileRequest(Name: name, TimeZone: zone));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_RejectsFutureBirthDate()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.PatchAsync("/api/users/me/",
            new UpdateProfileRequest(DateOfBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_TrimsOnlySuppliedFields()
    {
        await using var app = new EndpointTestContext();
        var created = await app.RegisterAsync(name: "Anna", surname: "Nowak");
        await app.LoginAsync(created.Email, "Haslo123!");

        using var response = await app.PatchAsync("/api/users/me/", new UpdateProfileRequest(Name: "  Maria  "));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await app.ReadAsync<UserResponse>(response);
        Assert.Equal("Maria", updated.Name);
        Assert.Equal("Nowak", updated.Surname);
    }

    [Fact]
    public async Task ChangePassword_ReplacesPasswordAndInvalidatesOldSession()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();

        using var response = await app.Client.PostAsJsonAsync("/api/users/me/change-password",
            new ChangePasswordRequest("Jakub123!", "NewPassword123!"), app.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var oldSession = await app.Client.GetAsync("/api/users/me/", app.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
        using var oldPassword = await app.Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("jakub@example.com", "Jakub123!"), app.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        var newTokens = await app.LoginAsync(password: "NewPassword123!");
        Assert.NotEmpty(newTokens.AccessToken);
    }

    [Fact]
    public async Task ChangePassword_RejectsIncorrectCurrentPassword()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.PostAsJsonAsync("/api/users/me/change-password",
            new ChangePasswordRequest("Wrong123!", "NewPassword123!"), app.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_RejectsTooShortNewPassword()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();
        using var response = await app.Client.PostAsJsonAsync("/api/users/me/change-password",
            new ChangePasswordRequest("Jakub123!", "short"), app.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_RemovesUserAndInvalidatesLogin()
    {
        await using var app = new EndpointTestContext();
        var created = await app.RegisterAsync();
        await app.LoginAsync(created.Email, "Haslo123!");

        using var response = await app.Client.DeleteAsync("/api/users/me/", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var profile = await app.Client.GetAsync("/api/users/me/", app.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
        using var login = await app.Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(created.Email, "Haslo123!"), app.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_RequiresAuthentication()
    {
        await using var app = new EndpointTestContext();
        using var response = await app.Client.DeleteAsync("/api/users/me/", app.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_RemovesMeasurementsAndSessionsAndUnpairsDevice()
    {
        await using var app = new EndpointTestContext();
        await app.LoginAsync();

        using var response = await app.Client.DeleteAsync("/api/users/me/", app.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await app.Factory.InDatabaseAsync(async db =>
        {
            var ct = app.CancellationToken;
            Assert.False(await db.Users.AnyAsync(x => x.Id == EndpointTestContext.SeedUserId, ct));
            Assert.False(await db.Sessions.AnyAsync(x => x.UserId == EndpointTestContext.SeedUserId, ct));
            Assert.False(await db.Measurements.AnyAsync(x => x.UserId == EndpointTestContext.SeedUserId, ct));
            Assert.False(await db.RefreshSessions.AnyAsync(x => x.UserId == EndpointTestContext.SeedUserId, ct));
            var device = await db.Devices.SingleAsync(x => x.Id == "seed-device", ct);
            Assert.Null(device.UserId);
        });
    }
}
