using HeartRate.Api.Security;
using HeartRate.Domain.Entities;
using HeartRate.Infrastructure.MongoDb;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HeartRate.Api;

public static class DataSeeder
{
    public static async Task SeedAsync(HeartRateDbContext db, string password, CancellationToken ct = default)
    {
        if (!Validation.Password(password))
            throw new InvalidOperationException("Seed:Password musi mieć 8–128 znaków.");
        if (await db.Users.AnyAsync(x => x.EmailNormalized == "JAKUB@EXAMPLE.COM", ct)) return;

        var user = new User
        {
            Id = "seed-user-jakub", Name = "jakub", Email = "jakub@example.com",
            EmailNormalized = "JAKUB@EXAMPLE.COM", PasswordHash = ""
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        var now = DateTime.UtcNow;
        var device = new Device { Id = "seed-device", UserId = user.Id, Name = "Urządzenie demo" };
        var session = new Session
        {
            Id = "seed-session", UserId = user.Id, DeviceId = device.Id,
            Name = "Przykładowa sesja", Tags = ["demo"],
            StartedAtUtc = now.AddMinutes(-2), FinishedAtUtc = now.AddMinutes(-1)
        };

        db.Users.Add(user);
        db.Devices.Add(device);
        db.Sessions.Add(session);
        db.Measurements.AddRange(
            Measurement.Create("seed-normal-1", user.Id, null, now.AddDays(-2), 72),
            Measurement.Create("seed-normal-2", user.Id, null, now.AddDays(-1), 76),
            Measurement.Create("seed-normal-3", user.Id, null, now.AddMinutes(-5), 80));
        for (var i = 0; i < 60; i++)
        {
            var measurement = Measurement.Create($"seed-session-{i + 1:D2}", user.Id,
                session.Id, session.StartedAtUtc.AddSeconds(i), 80 + i % 20);
            measurement.DeviceId = device.Id;
            db.Measurements.Add(measurement);
        }
        await db.SaveChangesAsync(ct);
    }
}
