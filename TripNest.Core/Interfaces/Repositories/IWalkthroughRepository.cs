using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IWalkthroughRepository : IRepository<Walkthrough>
{
    Task<IEnumerable<Walkthrough>> GetByPropertyIdAsync(string propertyId);
}
