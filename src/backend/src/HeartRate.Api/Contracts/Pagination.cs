using Microsoft.EntityFrameworkCore;

namespace HeartRate.Api.Contracts;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record Pagination(int Page = 1, int PageSize = 50)
{
    public bool IsValid() => Page > 0 && PageSize is >= 1 and <= 100
        && (long)(Page - 1) * PageSize <= int.MaxValue;

    public async Task<PagedResponse<TResponse>> ReadAsync<TEntity, TResponse>(
        IQueryable<TEntity> query, Func<TEntity, TResponse> map, CancellationToken ct)
    {
        var count = await query.CountAsync(ct);
        var items = await query.Skip((Page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        return new(items.Select(map).ToList(), Page, PageSize, count);
    }
}
