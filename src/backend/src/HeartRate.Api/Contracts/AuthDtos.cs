namespace HeartRate.Api.Contracts;

public sealed record RegisterRequest(string? Name, string? Email, string? Password, string? Surname = null);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record RefreshRequest(string? RefreshToken);
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public sealed record TokenResponse(string AccessToken, DateTime ExpiresAtUtc, string RefreshToken);
