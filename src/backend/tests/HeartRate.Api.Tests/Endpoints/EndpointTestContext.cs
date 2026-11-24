using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HeartRate.Api.Contracts;

namespace HeartRate.Api.Tests.Endpoints;

internal sealed class EndpointTestContext : IAsyncDisposable
{
    private readonly CancellationTokenSource timeout = new(TimeSpan.FromMinutes(2));

    public EndpointTestContext()
    {
        Factory = new ApiFactory();
        Client = Factory.CreateClient();
    }

    public const string SeedUserId = "seed-user-jakub";
    public ApiFactory Factory { get; }
    public HttpClient Client { get; }
    public CancellationToken CancellationToken => timeout.Token;

    public async Task<UserResponse> RegisterAsync(string? name = "Anna", string? email = null,
        string? password = "Haslo123!", string? surname = "Nowak")
    {
        email ??= $"user-{Guid.NewGuid():N}@example.com";
        using var response = await Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(name, email, password, surname), CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<UserResponse>(response);
    }

    public async Task<TokenResponse> LoginAsync(string email = "jakub@example.com", string password = "Jakub123!")
    {
        using var response = await Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, password), CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await ReadAsync<TokenResponse>(response);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return tokens;
    }

    public async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: CancellationToken);
        Assert.NotNull(value);
        return value;
    }

    public async Task<HttpResponseMessage> PatchAsync<T>(string path, T body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = JsonContent.Create(body)
        };
        return await Client.SendAsync(request, CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        timeout.Dispose();
    }
}
