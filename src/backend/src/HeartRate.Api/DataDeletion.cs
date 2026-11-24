using HeartRate.Infrastructure.MongoDb;
using Microsoft.EntityFrameworkCore;

namespace HeartRate.Api;

public static class DataDeletion
{
    // Małe porcje, żeby usuwanie długiej sesji nie wczytywało wszystkich próbek do pamięci.
    public static async Task DeleteAsync<T>(HeartRateDbContext db, IQueryable<T> query, CancellationToken ct)
        where T : class
    {
        while (true)
        {
            var items = await query.Take(500).ToListAsync(ct);
            if (items.Count == 0) return;
            db.RemoveRange(items);
            await db.SaveChangesAsync(ct);
        }
    }
}
