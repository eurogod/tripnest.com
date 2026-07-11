using TripNest.Core.DTOs.Search;
using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IPropertyRepository : IRepository<Property>
{
    Task<IEnumerable<Property>> GetByUserIdAsync(string userId);
    Task<IEnumerable<Property>> GetAllActiveAsync();
    Task<(IReadOnlyList<Property> Items, int TotalCount)> SearchPageAsync(PropertySearchCriteria criteria, int page, int pageSize);
}
