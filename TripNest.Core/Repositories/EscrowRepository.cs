using Microsoft.EntityFrameworkCore;
using TripNest.Core.Context;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Models;

namespace TripNest.Core.Repositories;

public class EscrowRepository : Repository<Escrow>, IEscrowRepository
{
    public EscrowRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Escrow?> GetByBookingIdAsync(string bookingId)
    {
        return await _context.Set<Escrow>()
            .FirstOrDefaultAsync(e => e.BookingId == bookingId);
    }
}
