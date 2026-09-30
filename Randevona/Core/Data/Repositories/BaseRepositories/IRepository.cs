using Domain.Entities.BaseEntities;
using MongoDB.Driver;
using System.Linq.Expressions;
namespace Data.Repositories.BaseRepositories;
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<T?> FindOneAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(int limit = 1000, CancellationToken ct = default);
    Task<(IReadOnlyList<T> Items, long TotalCount)> GetPagedAsync(int page, int pageSize,
        Expression<Func<T, bool>>? predicate = null, Expression<Func<T, object>>? orderBy = null,
        bool ascending = true, CancellationToken ct = default);
    Task<T> CreateAsync(T entity, CancellationToken ct = default);
    Task<IReadOnlyList<T>> CreateManyAsync(IEnumerable<T> entities, CancellationToken ct = default);
    Task<bool> UpdateAsync(T entity, CancellationToken ct = default);
    Task<long> UpdateManyAsync(Expression<Func<T, bool>> filter, UpdateDefinition<T> update, CancellationToken ct = default);
    Task UpsertAsync(T entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
    Task<long> DeleteManyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}
