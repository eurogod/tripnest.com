using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IMaintenanceRepository : IRepository<Maintenance>
{
    Task<IEnumerable<Maintenance>> GetByPropertyIdAsync(string propertyId);
    Task<IEnumerable<Maintenance>> GetByUserIdAsync(string userId);
}
