using Microsoft.EntityFrameworkCore;
using TripNest.Core.Context;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Models;

namespace TripNest.Core.Repositories;

public class AgreementRepository : Repository<Agreement>, IAgreementRepository
{
    public AgreementRepository(AppDbContext context) : base(context) { }

    public async Task<Agreement?> GetByBookingIdAsync(string bookingId)
    {
        return await _context.Set<Agreement>()
            .FirstOrDefaultAsync(a => a.BookingId == bookingId);
    }
}

public class MaintenanceRepository : Repository<Maintenance>, IMaintenanceRepository
{
    public MaintenanceRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Maintenance>> GetByPropertyIdAsync(string propertyId)
    {
        return await _context.Set<Maintenance>()
            .Where(m => m.PropertyId == propertyId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Maintenance>> GetByUserIdAsync(string userId)
    {
        return await _context.Set<Maintenance>()
            .Where(m => m.ReportedByUserId == userId)
            .ToListAsync();
    }
}

public class ReviewRepository : Repository<Review>, IReviewRepository
{
    public ReviewRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Review>> GetByRevieweeIdAsync(string revieweeId)
    {
        return await _context.Set<Review>()
            .Where(r => r.RevieweeId == revieweeId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Review>> GetByPropertyIdAsync(string propertyId)
    {
        return await _context.Set<Review>()
            .Where(r => r.PropertyId == propertyId)
            .ToListAsync();
    }
}

public class NotificationRepository : Repository<Notification>, INotificationRepository
{
    public NotificationRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Notification>> GetByUserIdAsync(string userId)
    {
        return await _context.Set<Notification>()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Notification>> GetUnreadByUserIdAsync(string userId)
    {
        return await _context.Set<Notification>()
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }
}

public class CaretakerRepository : Repository<Caretaker>, ICaretakerRepository
{
    public CaretakerRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Caretaker>> GetByPropertyIdAsync(string propertyId)
    {
        return await _context.Set<Caretaker>()
            .Where(c => c.PropertyId == propertyId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Caretaker>> GetByUserIdAsync(string userId)
    {
        return await _context.Set<Caretaker>()
            .Where(c => c.UserId == userId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Caretaker>> SearchByNameAsync(string? query, int take)
    {
        var q = _context.Set<Caretaker>().AsNoTracking().Include(c => c.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var ql = query.ToLower();
            q = q.Where(c => c.User != null && c.User.FullName.ToLower().Contains(ql));
        }
        return await q.Take(take).ToListAsync();
    }
}
