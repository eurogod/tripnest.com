using Microsoft.EntityFrameworkCore;
using TripNest.Core.Context;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Models;

namespace TripNest.Core.Repositories;

public class BookingRepository : Repository<Booking>, IBookingRepository
{
    public BookingRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Booking>> GetByTenantIdAsync(string tenantId)
    {
        return await _context.Set<Booking>()
            .Where(b => b.TenantId == tenantId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Booking>> GetByPropertyIdAsync(string propertyId)
    {
        return await _context.Set<Booking>()
            .Where(b => b.PropertyId == propertyId)
            .ToListAsync();
    }

    public async Task<Booking?> GetByIdWithDetailsAsync(string bookingId)
    {
        return await _context.Set<Booking>()
            .Include(b => b.Tenant)
            .Include(b => b.Property)
            .FirstOrDefaultAsync(b => b.Id == bookingId);
    }
}
