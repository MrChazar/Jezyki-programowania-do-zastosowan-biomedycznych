using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HeartRate.Api.Contracts;
using HeartRate.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HeartRate.Api.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "HeartRate.Api";
    public string Audience { get; set; } = "HeartRate.Client";
    public string Key { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
}

public sealed class TokenService(IOptions<JwtOptions> options)
{
    public TokenResponse Issue(User user, RefreshSession session, string refreshToken)
    {
        var settings = options.Value;
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(settings.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim("sub", user.Id),
            new Claim("sid", session.Id),
            new Claim("ver", user.TokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim("name", user.Name),
            new Claim("jti", Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            now, expires, new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256));

        return new(new JwtSecurityTokenHandler().WriteToken(token), expires, refreshToken);
    }

    public static string NewRefreshToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
