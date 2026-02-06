using Domain;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace EntityFrameworkCore.Repositories;

abstract class BaseRepository<T>(ApplicationDbContext context) : IBaseRepository<T> where T : Entity
{
    protected readonly ApplicationDbContext _context = context;

    public virtual void Add(T entity) => _context.Add(entity);

    public virtual async Task<T?> Find(
        Expression<Func<T, bool>> expression,
        bool forUpdate = false,
        string[]? includes = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _context.Set<T>();
        if (!forUpdate) query = query.AsNoTracking();
        if (includes != null)
            foreach (var path in includes) query = query.Include(path);

        return await query.FirstOrDefaultAsync(expression, cancellationToken);
    }

    public virtual async Task<IReadOnlyCollection<T>> GetAll(CancellationToken cancellationToken = default)
        => await _context.Set<T>()
        .AsNoTracking()
        .ToListAsync(cancellationToken);

    public virtual void Update(T entity) 
    {
        _context.Update(entity);
    }

    public Task Reload(
        T entity,
        CancellationToken cancellationToken = default) =>
        _context.Entry(entity)
        .ReloadAsync(cancellationToken);

    public virtual void Remove(T entity) => _context.Remove(entity);
}
