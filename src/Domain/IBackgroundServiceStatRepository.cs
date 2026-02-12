
using System.Linq.Expressions;

namespace Domain;

public interface IBackgroundServiceStatRepository : IRepository<BackgroundServiceStat>
{
    Task<BackgroundServiceStat> AddOrUpdate(
        string? details, 
        int total,
        BackgroundServiceStatType type,
        CancellationToken cancellationToken = default);

    Task<BackgroundServiceStatDto?> Get(
        Expression<Func<BackgroundServiceStat, bool>> expression, 
        CancellationToken cancellationToken = default);
}
