using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IReceiptRepository : IRepository<Receipt>
{
    Task<IEnumerable<Receipt>> GetByUserIdAsync(string userId);
    Task<IEnumerable<Receipt>> GetByBookingIdAsync(string bookingId);
}
