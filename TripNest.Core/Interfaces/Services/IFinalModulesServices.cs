namespace TripNest.Core.Interfaces.Services;

public interface ISmsSender
{
    /// <summary>Sends an SMS; returns true on success, false if not configured or the send failed.</summary>
    Task<bool> SendSmsAsync(string phoneNumber, string message);
}

public interface IEmailSender
{
    /// <summary>Sends an email; returns true on success, false if not configured or the send failed.</summary>
    Task<bool> SendAsync(string toEmail, string subject, string htmlBody);
}

public interface IWhatsAppSender
{
    /// <summary>Sends a WhatsApp message; returns true on success, false if not configured or it failed.</summary>
    Task<bool> SendAsync(string phoneNumber, string message);
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
