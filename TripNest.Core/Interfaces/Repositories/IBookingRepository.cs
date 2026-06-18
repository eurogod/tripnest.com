using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IBookingRepository : IRepository<Booking>
{
    Task<IEnumerable<Booking>> GetByTenantIdAsync(string tenantId);
    Task<IEnumerable<Booking>> GetByPropertyIdAsync(string propertyId);
    Task<Booking?> GetByIdWithDetailsAsync(string bookingId);
}
