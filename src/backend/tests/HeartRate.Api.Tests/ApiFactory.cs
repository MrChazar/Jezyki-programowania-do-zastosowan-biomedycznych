using System.Net.Http.Headers;
using System.Net.Http.Json;
using HeartRate.Api.Contracts;
using HeartRate.Infrastructure.MongoDb;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MongoDB.Driver;

namespace HeartRate.Api.Tests;

public sealed class ApiFactory(int authPermitLimit = 100) : WebApplicationFactory<Program>
{
    private readonly string databaseName = "heart_rate_tests_" + Guid.NewGuid().ToString("N");
    private static readonly string connectionString = Environment.GetEnvironmentVariable("HEARTRATE_TEST_MONGODB")
        ?? "mongodb://127.0.0.1:27017/?serverSelectionTimeoutMS=5000";
    private static readonly IMongoClient sharedClient = new MongoClient(connectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMongoClient>();
            services.AddSingleton(sharedClient);
            services.AddDbContext<HeartRateDbContext>((_, options) =>
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
        });
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MongoDb:ConnectionString"] = connectionString,
            ["MongoDb:DatabaseName"] = databaseName,
            ["Jwt:Key"] = "HeartRate-Integration-Test-Key-At-Least-32-Characters",
            ["Seed:Enabled"] = "true",
            ["Seed:Password"] = "Jakub123!",
            ["RateLimits:AuthPermitLimit"] = authPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }));
    }

    public async Task InDatabaseAsync(Func<HeartRateDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<HeartRateDbContext>());
    }

    public static async Task<TokenResponse> LoginAsync(
        HttpClient client, string email = "jakub@example.com", string password = "Jakub123!")
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return tokens;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await sharedClient.DropDatabaseAsync(databaseName);
    }
}
