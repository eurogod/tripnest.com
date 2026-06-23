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

    Task<T> AddAsync(T entity);

    Task<T> UpdateAsync(T entity);

    Task DeleteAsync(T entity);

    Task<int> SaveChangesAsync();
}
