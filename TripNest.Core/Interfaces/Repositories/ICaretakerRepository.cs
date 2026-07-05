using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface ICaretakerRepository : IRepository<Caretaker>
{
    Task<IEnumerable<Caretaker>> GetByPropertyIdAsync(string propertyId);
    Task<IEnumerable<Caretaker>> GetByUserIdAsync(string userId);

    /// <summary>
    /// Name search with the owning user eager-loaded, filtered and limited in the database.
    /// A null/blank query returns the first <paramref name="take"/> caretakers.
    /// </summary>
    Task<IReadOnlyList<Caretaker>> SearchByNameAsync(string? query, int take);
}
