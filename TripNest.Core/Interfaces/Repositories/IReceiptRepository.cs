using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IReceiptRepository : IRepository<Receipt>
{
    Task<IEnumerable<Receipt>> GetByUserIdAsync(string userId);
    Task<IEnumerable<Receipt>> GetByBookingIdAsync(string bookingId);

    /// <summary>All receipts for the given bookings in a single query (avoids a per-booking N+1).</summary>
    Task<IEnumerable<Receipt>> GetByBookingIdsAsync(IEnumerable<string> bookingIds);
}
