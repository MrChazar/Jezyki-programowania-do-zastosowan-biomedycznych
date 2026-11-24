namespace HeartRate.Domain.Entities;

public sealed class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Name { get; set; }
    public string Surname { get; set; } = "";
    public DateOnly? DateOfBirth { get; set; }
    public required string Email { get; set; }
    public required string EmailNormalized { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string TimeZone { get; set; } = "Europe/Warsaw";
    // Zwiększenie tej liczby unieważnia wszystkie tokeny użytkownika.
    public int TokenVersion { get; set; }
}
