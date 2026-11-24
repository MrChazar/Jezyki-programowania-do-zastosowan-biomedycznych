using HeartRate.Infrastructure.MongoDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HeartRate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<MongoOptions>()
            .Bind(configuration.GetRequiredSection(MongoOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "MongoDb:ConnectionString is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.DatabaseName),
                "MongoDb:DatabaseName is required.")
            .ValidateOnStart();

        services.AddSingleton<IMongoClient>(provider => new MongoClient(
            provider.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));
        services.AddDbContext<HeartRateDbContext>((provider, options) => options.UseMongoDB(
            provider.GetRequiredService<IMongoClient>(),
            provider.GetRequiredService<IOptions<MongoOptions>>().Value.DatabaseName));
        return services;
    }
}
