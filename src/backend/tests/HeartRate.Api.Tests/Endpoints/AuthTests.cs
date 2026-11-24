using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HeartRate.Api.Contracts;

namespace HeartRate.Api.Tests.Endpoints;

public sealed class AuthTests
{
    [Fact]
    public async Task Register_CreatesUserWithoutAuthenticatingClient()
    {
        await using var app = new EndpointTestContext();
        var user = await app.RegisterAsync(email: "new@example.com");

        Assert.Equal("Anna", user.Name);
        Assert.Equal("Nowak", user.Surname);
        Assert.Equal("new@example.com", user.Email);
        using var profile = await app.Client.GetAsync("/api/users/me/", app.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
    }

    [Theory]
    [InlineData(" ", "valid@example.com", "Haslo123!")]
    [InlineData("Anna", "invalid", "Haslo123!")]
    [InlineData("Anna", "valid@example.com", "short")]
    public async Task Register_RejectsInvalidInput(string name, string email, string password)
    {
        await using var app = new EndpointTestContext();
        using var response = await app.Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(name, email, password), app.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_RejectsDuplicateEmailIgnoringCase()
    {
        await using var app = new EndpointTestContext();
        await app.RegisterAsync(email: "Mixed@example.com");

        using var response = await app.Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Other", "mixed@EXAMPLE.com", "Haslo123!"), app.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsUsableAccessAndRefreshTokens()
    {
        await using var app = new EndpointTestContext();
        var tokens = await app.LoginAsync();

        Assert.NotEmpty(tokens.AccessToken);
        Assert.Equal(64, tokens.RefreshToken.Length);
        using var profile = await app.Client.GetAsync("/api/users/me/", app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    [Fact]
    public async Task Login_RejectsIncorrectPassword()
    {
        await using var app = new EndpointTestContext();
        using var response = await app.Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("jakub@example.com", "Incorrect123!"), app.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_AcceptsEmailWithDifferentCaseAndSurroundingWhitespace()
    {
        await using var app = new EndpointTestContext();
        var tokens = await app.LoginAsync(" JAKUB@EXAMPLE.COM ");
        Assert.NotEmpty(tokens.AccessToken);
    }

    [Fact]
    public async Task Refresh_RotatesTokenAndRejectsReuse()
    {
        await using var app = new EndpointTestContext();
        var original = await app.LoginAsync();

        using var response = await app.Client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(original.RefreshToken), app.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rotated = await app.ReadAsync<TokenResponse>(response);
        Assert.NotEqual(original.RefreshToken, rotated.RefreshToken);

        using var reuse = await app.Client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(original.RefreshToken), app.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_RejectsMalformedToken()
    {
        await using var app = new EndpointTestContext();
        using var response = await app.Client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest("short"), app.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RejectsUnknownTokenOfCorrectLength()
    {
        await using var app = new EndpointTestContext();
        using var response = await app.Client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(new string('A', 64)), app.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesCurrentSession()
    {
        await using var app = new EndpointTestContext();
        var tokens = await app.LoginAsync();

        using var response = await app.Client.PostAsync("/api/auth/logout", null, app.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var profile = await app.Client.GetAsync("/api/users/me/", app.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
        using var refresh = await app.Client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(tokens.RefreshToken), app.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_RequiresAuthentication()
    {
        await using var app = new EndpointTestContext();
        using var response = await app.Client.PostAsync("/api/auth/logout", null, app.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LogoutAll_InvalidatesEveryExistingSession()
    {
        await using var app = new EndpointTestContext();
        var first = await app.LoginAsync();
        var second = await app.LoginAsync();

        using var response = await app.Client.PostAsync("/api/auth/logout-all", null, app.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        foreach (var token in new[] { first.AccessToken, second.AccessToken })
        {
            app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var profile = await app.Client.GetAsync("/api/users/me/", app.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
        }
    }

    [Fact]
    public async Task LogoutAll_RequiresAuthentication()
    {
        await using var app = new EndpointTestContext();
        using var response = await app.Client.PostAsync("/api/auth/logout-all", null, app.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
