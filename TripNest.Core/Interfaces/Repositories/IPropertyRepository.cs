using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IPropertyRepository : IRepository<Property>
{
    Task<IEnumerable<Property>> GetByUserIdAsync(string userId);
    Task<IEnumerable<Property>> GetAllActiveAsync();
    Task<(IReadOnlyList<Property> Items, int TotalCount)> SearchPageAsync(string location, int minBedrooms, int maxBedrooms, int page, int pageSize);
}
