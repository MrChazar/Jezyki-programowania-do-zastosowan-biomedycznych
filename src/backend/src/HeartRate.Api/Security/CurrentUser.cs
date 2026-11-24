using System.Security.Claims;

namespace HeartRate.Api.Security;

public static class CurrentUser
{
    public static string UserId(this ClaimsPrincipal user) => user.FindFirstValue("sub")!;
    public static string RefreshSessionId(this ClaimsPrincipal user) => user.FindFirstValue("sid")!;
}
