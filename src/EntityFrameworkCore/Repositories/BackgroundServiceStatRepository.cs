using Domain;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace EntityFrameworkCore.Repositories;

class BackgroundServiceStatRepository(
    ApplicationDbContext context,
    TimeProvider timeProvider) : BaseRepository<BackgroundServiceStat>(context), IBackgroundServiceStatRepository
{
    public async Task<BackgroundServiceStat> AddOrUpdate(
       string? details,
       int total,
       BackgroundServiceStatType type,
       CancellationToken cancellationToken = default)
    {
        var stat = await Find(
            bss => bss.Type == type,
            true,
            [],
            cancellationToken) ?? new BackgroundServiceStat()
            {
                ExternalId = Guid.NewGuid(),
                Type = type
            };

        stat.Details = details;
        stat.LastRunAt = timeProvider.GetUtcNow();
        stat.Total += total;
        stat.TotalInLastRun = total;

        if (_context.Entry(stat).State == EntityState.Detached)
        {
            Add(stat);
        }
           
        return stat;
    }

    public Task<BackgroundServiceStatDto?> Get(
        Expression<Func<BackgroundServiceStat, bool>> expression,
        CancellationToken cancellationToken = default)
    {
        return _context.Set<BackgroundServiceStat>().Where(expression)
            .Select(bss => new BackgroundServiceStatDto(
                bss.ExternalId,
                bss.Details,
                bss.LastRunAt,
                bss.Total,
                bss.TotalInLastRun,
                bss.Type))
            .FirstOrDefaultAsync(cancellationToken);
    }
}