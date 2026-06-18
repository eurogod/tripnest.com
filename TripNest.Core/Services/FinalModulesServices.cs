using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class TrustScoreService : ITrustScoreService
{
    private readonly ITrustScoreSnapshotRepository _snapshotRepository;
    private readonly IStayFeedbackRepository _feedbackRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<TrustScoreService> _logger;

    public TrustScoreService(
        ITrustScoreSnapshotRepository snapshotRepository,
        IStayFeedbackRepository feedbackRepository,
        IBookingRepository bookingRepository,
        IPropertyRepository propertyRepository,
        IUserRepository userRepository,
        ILogger<TrustScoreService> logger)
    {
        _snapshotRepository = snapshotRepository;
        _feedbackRepository = feedbackRepository;
        _bookingRepository = bookingRepository;
        _propertyRepository = propertyRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<decimal> CalculatePropertyTrustScoreAsync(string propertyId)
    {
        var property = await _propertyRepository.GetByIdAsync(propertyId);
        if (property == null) return 0m;

        var verificationComponent = CalculatePropertyVerificationComponent(property);
        var historyComponent = await CalculatePropertyHistoryComponentAsync(propertyId);
        var feedbackComponent = await CalculatePropertyFeedbackComponentAsync(propertyId);

        return (verificationComponent * 0.5m) + (historyComponent * 0.3m) + (feedbackComponent * 0.2m);
    }

    public async Task<decimal> CalculateUserTrustScoreAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return 0m;

        var verificationComponent = CalculateUserVerificationComponent(user);
        var historyComponent = await CalculateUserHistoryComponentAsync(userId);
        var feedbackComponent = await CalculateUserFeedbackComponentAsync(userId);

        return (verificationComponent * 0.5m) + (historyComponent * 0.3m) + (feedbackComponent * 0.2m);
    }

    public async Task RecalculateNowAsync(string subjectType, string subjectId)
    {
        decimal verificationComponent, historyComponent, feedbackComponent;

        if (subjectType == "Property")
        {
            var property = await _propertyRepository.GetByIdAsync(subjectId);
            if (property == null) return;
            verificationComponent = CalculatePropertyVerificationComponent(property);
            historyComponent = await CalculatePropertyHistoryComponentAsync(subjectId);
            feedbackComponent = await CalculatePropertyFeedbackComponentAsync(subjectId);
        }
        else if (subjectType == "User")
        {
            var user = await _userRepository.GetByIdAsync(subjectId);
            if (user == null) return;
            verificationComponent = CalculateUserVerificationComponent(user);
            historyComponent = await CalculateUserHistoryComponentAsync(subjectId);
            feedbackComponent = await CalculateUserFeedbackComponentAsync(subjectId);
        }
        else
        {
            return;
        }

        var finalScore = (verificationComponent * 0.5m) + (historyComponent * 0.3m) + (feedbackComponent * 0.2m);

        var latest = await _snapshotRepository.GetLatestAsync(subjectType, subjectId);
        var trend = CalculateTrend(finalScore, latest?.FinalScore ?? 0m);

        var snapshot = new TrustScoreSnapshot
        {
            SubjectType = subjectType,
            SubjectId = subjectId,
            VerificationComponent = verificationComponent,
            HistoryComponent = historyComponent,
            FeedbackComponent = feedbackComponent,
            FinalScore = finalScore,
            Trend = trend,
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        await _snapshotRepository.AddAsync(snapshot);
        await _snapshotRepository.SaveChangesAsync();

        _logger.LogInformation("Trust score recalculated for {SubjectType} {SubjectId}: {Score}", subjectType, subjectId, finalScore);
    }

    private decimal CalculatePropertyVerificationComponent(Property property)
    {
        decimal score = 0m;
        if (property.Status.ToString() == "Active") score += 40m;
        score += 30m;
        return Math.Min(score, 100m);
    }

    private async Task<decimal> CalculatePropertyHistoryComponentAsync(string propertyId)
    {
        var bookings = await _bookingRepository.GetByPropertyIdAsync(propertyId);
        if (!bookings.Any()) return 50m;

        var completed = bookings.Count(b => b.Status == BookingStatus.Completed);
        var cancelled = bookings.Count(b => b.Status == BookingStatus.Cancelled);
        var completionRate = completed > 0 ? (decimal)completed / (completed + cancelled) : 0m;

        return (completionRate * 100m);
    }

    private async Task<decimal> CalculatePropertyFeedbackComponentAsync(string propertyId)
    {
        var feedback = await _feedbackRepository.GetByPropertyIdAsync(propertyId);
        if (!feedback.Any()) return 50m;

        var avgRating = feedback.Average(f => (f.AccuracyRating + f.CleanlinessRating + f.SafetyRating) / 3m);
        return (avgRating / 5m) * 100m;
    }

    private decimal CalculateUserVerificationComponent(User user)
    {
        decimal score = 0m;
        if (user.IsVerified) score += 50m;
        var accountAgeDays = (DateTime.UtcNow - user.CreatedAt).Days;
        score += Math.Min((accountAgeDays / 365m) * 50m, 50m);
        return Math.Min(score, 100m);
    }

    private async Task<decimal> CalculateUserHistoryComponentAsync(string userId)
    {
        var bookings = await _bookingRepository.GetByTenantIdAsync(userId);
        if (!bookings.Any()) return 50m;

        var completed = bookings.Count(b => b.Status == BookingStatus.Completed);
        var cancelled = bookings.Count(b => b.Status == BookingStatus.Cancelled);
        var completionRate = completed > 0 ? (decimal)completed / (completed + cancelled) : 0m;

        return completionRate * 100m;
    }

    private async Task<decimal> CalculateUserFeedbackComponentAsync(string userId)
    {
        var feedback = await _feedbackRepository.GetByLandlordIdAsync(userId);
        if (!feedback.Any()) return 50m;

        var avgRating = feedback.Average(f => (f.AccuracyRating + f.CleanlinessRating + f.SafetyRating) / 3m);
        return (avgRating / 5m) * 100m;
    }

    private TrustScoreTrend CalculateTrend(decimal newScore, decimal oldScore)
    {
        var diff = newScore - oldScore;
        if (diff > 1m) return TrustScoreTrend.Improving;
        if (diff < -1m) return TrustScoreTrend.Declining;
        return TrustScoreTrend.Stable;
    }
}

public class AuditService : IAuditService
{
    private readonly IAuditLogRepository _auditRepository;
    private readonly ILogger<AuditService> _logger;

    public AuditService(IAuditLogRepository auditRepository, ILogger<AuditService> logger)
    {
        _auditRepository = auditRepository;
        _logger = logger;
    }

    public async Task LogActionAsync(string userId, string action, string entityType, string entityId, string? oldValue = null, string? newValue = null, string? ipAddress = null)
    {
        var log = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValue = oldValue,
            NewValue = newValue,
            IpAddress = ipAddress
        };

        await _auditRepository.AddAsync(log);
        await _auditRepository.SaveChangesAsync();

        _logger.LogInformation("Audit: {Action} on {EntityType} {EntityId} by {UserId}", action, entityType, entityId, userId);
    }
}

public class SmsSender : ISmsSender
{
    private readonly ILogger<SmsSender> _logger;

    public SmsSender(ILogger<SmsSender> logger)
    {
        _logger = logger;
    }

    public Task SendSmsAsync(string phoneNumber, string message)
    {
        _logger.LogInformation("SMS would be sent to {PhoneNumber}: {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }
}
