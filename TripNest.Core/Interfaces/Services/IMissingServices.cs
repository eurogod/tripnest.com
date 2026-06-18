namespace TripNest.Core.Interfaces.Services;

public interface IEscrowService
{
    Task<object> InitiatePaymentAsync(string bookingId, decimal amount);
    Task VerifyAndHoldPaymentAsync(string bookingId, string reference);
    Task<object?> GetEscrowAsync(string escrowId, string userId);
    Task ReleaseEscrowAsync(string escrowId, string userId);
    Task RaiseDisputeAsync(string escrowId, string userId, string reason);
    Task ResolveDisputeAsync(string escrowId, bool approved);
    Task RefundEscrowAsync(string escrowId, string reason);
}

public interface IAgreementService
{
    Task<object> CreateAgreementAsync(string bookingId, string userId);
    Task<List<object>> GetUserAgreementsAsync(string userId);
    Task<object?> GetAgreementAsync(string agreementId, string userId);
    Task SignAgreementAsync(string agreementId, string userId);
    Task<(byte[], string)> DownloadAgreementPdfAsync(string agreementId, string userId);
}

public interface ICaretakerService
{
    Task<List<object>> GetAvailableCaretakersAsync(string? serviceType, string? area);
    Task<object?> GetCaretakerProfileAsync(string caretakerId);
    Task AssignCaretakerToPropertyAsync(string propertyId, string caretakerId, string landlordId);
    Task<object> CreateServiceRequestAsync(object request, string userId);
    Task<List<object>> GetServiceRequestsAsync(string userId);
    Task AcceptServiceRequestAsync(string requestId, string caretakerId);
    Task UpdateServiceRequestStatusAsync(string requestId, string status, string userId);
    Task SubmitServiceReviewAsync(string requestId, string userId, int rating, string? comment);
}

public interface IMaintenanceService
{
    Task<object> ReportMaintenanceAsync(object request, string tenantId);
    Task<List<object>> GetPropertyMaintenanceAsync(string propertyId, string landlordId);
    Task<List<object>> GetTenantMaintenanceAsync(string tenantId);
    Task UpdateMaintenanceStatusAsync(string maintenanceId, string status, string userId);
    Task<object> ConvertToServiceRequestAsync(string maintenanceId, string? caretakerId, string landlordId);
}

public interface IAgentService
{
    Task<List<object>> GetVerifiedAgentsAsync(string? serviceArea);
    Task<object?> GetAgentProfileAsync(string agentId);
    Task<object> CreateViewingRequestAsync(string agentId, string propertyId, DateTime scheduledAt, string tenantId, string? notes);
    Task UpdateViewingRequestStatusAsync(string requestId, string status, string userId);
}

public interface IReviewService
{
    Task<object> CreateReviewAsync(string bookingId, string propertyId, string reviewerId, int rating, string? comment);
    Task<object> GetPropertyReviewsAsync(string propertyId, int page, int pageSize);
    Task<List<object>> GetUserReviewsAsync(string userId);
    Task<object?> GetReviewAsync(string reviewId);
    Task DeleteReviewAsync(string reviewId, string userId);
}

public interface INotificationService
{
    Task<object> GetUserNotificationsAsync(string userId, int page, int pageSize);
    Task MarkAsReadAsync(string notificationId, string userId);
    Task MarkAllAsReadAsync(string userId);
    Task<int> GetUnreadCountAsync(string userId);
    Task DeleteNotificationAsync(string notificationId, string userId);
}

public interface IReceiptService
{
    Task<object> GetUserReceiptsAsync(string userId, int page, int pageSize);
    Task<object?> GetReceiptAsync(string receiptId, string userId);
    Task<(byte[], string)> DownloadReceiptPdfAsync(string receiptId, string userId);
    Task<object?> GetReceiptByBookingAsync(string bookingId, string userId);
}

public interface IChatService
{
    Task<List<object>> GetUserConversationsAsync(string userId);
    Task<object> StartConversationAsync(string userId, string otherUserId, string? propertyId);
    Task<object?> GetConversationAsync(string conversationId, string userId);
    Task<object> GetConversationMessagesAsync(string conversationId, string userId, int page, int pageSize);
    Task<object> SendMessageAsync(string conversationId, string userId, string body);
    Task MarkMessageAsReadAsync(string messageId, string userId);
    Task MarkConversationAsReadAsync(string conversationId, string userId);
    Task DeleteConversationAsync(string conversationId, string userId);
}
