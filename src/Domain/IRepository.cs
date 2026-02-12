using System.Linq.Expressions;

namespace Domain;

public interface IRepository<T> where T : Entity
{
    void Add(T entity);

    Task<T?> Find(
        Expression<Func<T, bool>> expression,
        bool forUpdate = false,
        string[]? includes = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<T>> GetAll(CancellationToken cancellationToken = default);

    void Update(T entity);

    Task Reload(
        T entity,
        CancellationToken cancellationToken = default);

    void Remove(T entity);

    Task<int> ExecuteDelete(
        Expression<Func<T, bool>> expression, 
        int batchSize, 
        CancellationToken cancellationToken = default);
}
