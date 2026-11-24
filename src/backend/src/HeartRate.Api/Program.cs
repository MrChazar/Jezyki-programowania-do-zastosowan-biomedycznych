using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using HeartRate.Api;
using HeartRate.Api.Endpoints;
using HeartRate.Api.Security;
using HeartRate.Domain.Entities;
using HeartRate.Infrastructure;
using HeartRate.Infrastructure.MongoDb;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.Services.AddApiDocumentation();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddOptions<JwtOptions>().BindConfiguration("Jwt")
    .Validate(x => x.Key.Length >= 32, "Ustaw Jwt:Key (minimum 32 znaki).")
    .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer) && !string.IsNullOrWhiteSpace(x.Audience), "Ustaw Jwt:Issuer i Jwt:Audience.")
    .Validate(x => x.AccessTokenMinutes is >= 1 and <= 60 && x.RefreshTokenDays is >= 1 and <= 90,
        "Czas ważności tokenów jest niepoprawny.")
    .ValidateOnStart();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwt.Issuer,
            ValidateAudience = true, ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(10),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256], NameClaimType = "name"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirstValue("sub");
                var sessionId = context.Principal?.FindFirstValue("sid");
                if (userId is null || sessionId is null
                    || !int.TryParse(context.Principal?.FindFirstValue("ver"), out var version))
                {
                    context.Fail("Niepoprawny token.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<HeartRateDbContext>();
                var ct = context.HttpContext.RequestAborted;
                var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, ct);
                var now = DateTime.UtcNow;
                if (user is null || user.TokenVersion != version
                    || !await db.RefreshSessions.AnyAsync(x => x.Id == sessionId && x.UserId == userId
                        && x.TokenVersion == version && x.RevokedAtUtc == null && x.ExpiresAtUtc > now, ct))
                    context.Fail("Sesja wygasła lub została unieważniona.");
            }
        };
    });
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimits:AuthPermitLimit", 10),
            Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
    options.AddPolicy("pair", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.UserId(), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});
builder.Services.AddCors(options => options.AddPolicy("WebClient", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseExceptionHandler();
app.UseCors("WebClient");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options => options.Title = "HeartRate API").AllowAnonymous();
}

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapDeviceEndpoints();
app.MapSessionEndpoints();
app.MapMeasurementEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HeartRateDbContext>();
    await db.Database.EnsureCreatedAsync();
    if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Seed:Enabled"))
        await DataSeeder.SeedAsync(db, app.Configuration["Seed:Password"] ?? "");
}

app.Run();

public partial class Program;
