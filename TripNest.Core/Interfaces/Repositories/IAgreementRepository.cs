using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IAgreementRepository : IRepository<Agreement>
{
    Task<Agreement?> GetByBookingIdAsync(string bookingId);
}
