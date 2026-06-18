using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IEscrowRepository : IRepository<Escrow>
{
    Task<Escrow?> GetByBookingIdAsync(string bookingId);
}
