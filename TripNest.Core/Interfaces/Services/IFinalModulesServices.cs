namespace TripNest.Core.Interfaces.Services;

public interface ISmsSender
{
    Task SendSmsAsync(string phoneNumber, string message);
}

public interface ITrustScoreService
{
    Task<decimal> CalculatePropertyTrustScoreAsync(string propertyId);
    Task<decimal> CalculateUserTrustScoreAsync(string userId);
    Task RecalculateNowAsync(string subjectType, string subjectId);
}

public interface IAuditService
{
    Task LogActionAsync(string userId, string action, string entityType, string entityId, string? oldValue = null, string? newValue = null, string? ipAddress = null);
}
