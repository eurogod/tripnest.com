using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface ISafetyCheckInRepository : IRepository<SafetyCheckIn>
{
    Task<SafetyCheckIn?> GetByBookingIdAsync(string bookingId);
}

public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<IEnumerable<AuditLog>> GetByUserIdAsync(string userId);
    Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityType, string entityId);

    /// <summary>Newest logs first, optionally filtered to one user, limited in SQL — the audit
    /// table grows without bound, so it must never be loaded whole.</summary>
    Task<IEnumerable<AuditLog>> GetRecentAsync(int limit, string? userId = null);
}

public interface ITrustScoreSnapshotRepository : IRepository<TrustScoreSnapshot>
{
    Task<TrustScoreSnapshot?> GetLatestAsync(string subjectType, string subjectId);
    Task<IEnumerable<TrustScoreSnapshot>> GetHistoryAsync(string subjectType, string subjectId, int days);
}

public interface IStayFeedbackRepository : IRepository<StayFeedback>
{
    Task<StayFeedback?> GetByBookingIdAsync(string bookingId);
    Task<IEnumerable<StayFeedback>> GetByPropertyIdAsync(string propertyId);
    Task<IEnumerable<StayFeedback>> GetByLandlordIdAsync(string landlordId);
}
