using HeartRate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;

namespace HeartRate.Infrastructure.MongoDb;

public sealed class HeartRateDbContext : DbContext
{
    public HeartRateDbContext(DbContextOptions<HeartRateDbContext> options) : base(options)
    {
        // Pozwala korzystać także z lokalnego MongoDB bez replica set.
        Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Measurement> Measurements => Set<Measurement>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().ToCollection("users");
        modelBuilder.Entity<User>().HasIndex(x => x.EmailNormalized).IsUnique();
        modelBuilder.Entity<User>().Property(x => x.TokenVersion).IsConcurrencyToken();
        modelBuilder.Entity<User>().Property(x => x.PasswordHash).IsConcurrencyToken();

        modelBuilder.Entity<Device>().ToCollection("devices");
        modelBuilder.Entity<Device>().HasIndex(x => x.UserId);
        modelBuilder.Entity<Device>().Property(x => x.UserId).IsConcurrencyToken();
        modelBuilder.Entity<Device>().Property(x => x.PairingCodeHash).IsConcurrencyToken();

        modelBuilder.Entity<Session>().ToCollection("sessions");
        modelBuilder.Entity<Session>().HasIndex(x => new { x.UserId, x.StartedAtUtc, x.Id });

        modelBuilder.Entity<Measurement>().ToCollection("measurements");
        modelBuilder.Entity<Measurement>().Ignore(x => x.IntervalSeconds);
        modelBuilder.Entity<Measurement>().HasIndex(x => new { x.UserId, x.MeasuredAtUtc, x.Id });
        modelBuilder.Entity<Measurement>().HasIndex(x => new { x.UserId, x.SessionId, x.MeasuredAtUtc, x.Id });

        modelBuilder.Entity<RefreshSession>().ToCollection("refresh_sessions");
        modelBuilder.Entity<RefreshSession>().HasIndex(x => x.TokenHash).IsUnique();
        modelBuilder.Entity<RefreshSession>().HasIndex(x => x.UserId);
        modelBuilder.Entity<RefreshSession>().Property(x => x.TokenHash).IsConcurrencyToken();
        modelBuilder.Entity<RefreshSession>().Property(x => x.RevokedAtUtc).IsConcurrencyToken();
    }
}
