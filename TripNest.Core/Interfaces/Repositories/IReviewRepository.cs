using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IReviewRepository : IRepository<Review>
{
    Task<IEnumerable<Review>> GetByRevieweeIdAsync(string revieweeId);
    Task<IEnumerable<Review>> GetByPropertyIdAsync(string propertyId);
}
