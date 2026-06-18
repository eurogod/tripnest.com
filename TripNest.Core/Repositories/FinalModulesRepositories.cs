using Microsoft.EntityFrameworkCore;
using TripNest.Core.Context;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Models;

namespace TripNest.Core.Repositories;

public class SafetyCheckInRepository : Repository<SafetyCheckIn>, ISafetyCheckInRepository
{
    public SafetyCheckInRepository(AppDbContext context) : base(context) { }

    public async Task<SafetyCheckIn?> GetByBookingIdAsync(string bookingId)
    {
        return await _context.Set<SafetyCheckIn>()
            .FirstOrDefaultAsync(s => s.BookingId == bookingId);
    }
}

public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<AuditLog>> GetByUserIdAsync(string userId)
    {
        return await _context.Set<AuditLog>()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityType, string entityId)
    {
        return await _context.Set<AuditLog>()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }
}

public class TrustScoreSnapshotRepository : Repository<TrustScoreSnapshot>, ITrustScoreSnapshotRepository
{
    public TrustScoreSnapshotRepository(AppDbContext context) : base(context) { }

    public async Task<TrustScoreSnapshot?> GetLatestAsync(string subjectType, string subjectId)
    {
        return await _context.Set<TrustScoreSnapshot>()
            .Where(t => t.SubjectType == subjectType && t.SubjectId == subjectId)
            .OrderByDescending(t => t.SnapshotDate)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TrustScoreSnapshot>> GetHistoryAsync(string subjectType, string subjectId, int days)
    {
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));
        return await _context.Set<TrustScoreSnapshot>()
            .Where(t => t.SubjectType == subjectType && t.SubjectId == subjectId && t.SnapshotDate >= startDate)
            .OrderBy(t => t.SnapshotDate)
            .ToListAsync();
    }
}

public class StayFeedbackRepository : Repository<StayFeedback>, IStayFeedbackRepository
{
    public StayFeedbackRepository(AppDbContext context) : base(context) { }

    public async Task<StayFeedback?> GetByBookingIdAsync(string bookingId)
    {
        return await _context.Set<StayFeedback>()
            .FirstOrDefaultAsync(s => s.BookingId == bookingId);
    }

    public async Task<IEnumerable<StayFeedback>> GetByPropertyIdAsync(string propertyId)
    {
        return await _context.Set<StayFeedback>()
            .Where(s => s.PropertyId == propertyId)
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StayFeedback>> GetByLandlordIdAsync(string landlordId)
    {
        return await _context.Set<StayFeedback>()
            .Where(s => s.LandlordId == landlordId)
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync();
    }
}
