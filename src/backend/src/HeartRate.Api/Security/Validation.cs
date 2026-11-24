using System.Net.Mail;

namespace HeartRate.Api.Security;

public static class Validation
{
    public static bool Email(string? email) => email is { Length: <= 254 }
        && MailAddress.TryCreate(email.Trim(), out var address) && address.Address == email.Trim();

    public static bool Password(string? password) => password is { Length: >= 8 and <= 128 };
    public static bool Name(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 100;

    public static bool TimeZone(string timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone) || timeZone.Length > 100)
            return false;
        try { TimeZoneInfo.FindSystemTimeZoneById(timeZone); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }
}
