using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IPropertyRepository : IRepository<Property>
{
    Task<IEnumerable<Property>> GetByUserIdAsync(string userId);
    Task<IEnumerable<Property>> GetAllActiveAsync();
    Task<IEnumerable<Property>> SearchAsync(string location, int minBedrooms, int maxBedrooms);
}
