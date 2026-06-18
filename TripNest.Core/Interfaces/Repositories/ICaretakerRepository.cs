using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface ICaretakerRepository : IRepository<Caretaker>
{
    Task<IEnumerable<Caretaker>> GetByPropertyIdAsync(string propertyId);
    Task<IEnumerable<Caretaker>> GetByUserIdAsync(string userId);
}
