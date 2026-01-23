using System.Linq.Expressions;

namespace Domain;

public interface IBaseRepository<T> where T : Entity
{
    void Add(T entity);

    Task<T?> Find(
        Expression<Func<T, bool>> expression,
        bool forUpdate = false,
        string[]? includes = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<T>> GetAll(CancellationToken cancellationToken = default);

    void Update(T entity);

    void Remove(T entity);
}
