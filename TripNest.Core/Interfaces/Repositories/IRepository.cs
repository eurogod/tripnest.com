using System.Linq.Expressions;

namespace TripNest.Core.Interfaces.Repositories;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(string id);

    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>Filtered query pushed to the database (no whole-table load).</summary>
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

    /// <summary>Counts matching rows in the database without materialising them.</summary>
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// One paged query: filter, order, count and slice all run in the database
    /// (ORDER BY / OFFSET / LIMIT), so only the requested page is materialised.
    /// </summary>
    Task<(IReadOnlyList<T> Items, int TotalCount)> FindPageAsync(
        Expression<Func<T, bool>>? predicate,
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
        int page,
        int pageSize);

    Task<T> AddAsync(T entity);

    Task<T> UpdateAsync(T entity);

    Task DeleteAsync(T entity);

    Task<int> SaveChangesAsync();
}
