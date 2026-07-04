using TripNest.Core.DTOs.Agents;
using TripNest.Core.DTOs.Agreements;
using TripNest.Core.DTOs.Caretakers;
using TripNest.Core.DTOs.Chat;
using TripNest.Core.DTOs.Escrow;
using TripNest.Core.DTOs.Maintenance;
using TripNest.Core.DTOs.Notifications;
using TripNest.Core.DTOs.Receipts;
using TripNest.Core.DTOs.Reviews;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Enums;

namespace TripNest.Core.Interfaces.Services;

public interface IEscrowService
{
    Task<EscrowResponse> InitiatePaymentAsync(string bookingId, string userId);
    // paidAmount: amount actually paid (major currency units) from the signature-verified provider
    // webhook, so the service can reject under/over-payment before holding funds.
    Task VerifyAndHoldPaymentAsync(string bookingId, string reference, decimal paidAmount);
    // Fallback/reconcile confirmation: actively asks the provider whether the booking's payment
    // succeeded (rather than waiting for the webhook) and holds the funds if so. Idempotent.
    Task<EscrowResponse> VerifyPaymentByBookingAsync(string bookingId, string userId);
    Task<EscrowResponse?> GetEscrowAsync(string escrowId, string userId);
    Task<EscrowResponse?> GetEscrowByBookingAsync(string bookingId, string userId);
    // All escrows for the caller's own bookings (as the paying tenant), newest first.
    Task<List<EscrowResponse>> GetMyEscrowsAsync(string userId);
    Task ReleaseEscrowAsync(string escrowId, string userId);
    Task RaiseDisputeAsync(string escrowId, string userId, string reason);
    Task ResolveDisputeAsync(string escrowId, bool approved);
    Task RefundEscrowAsync(string escrowId, string reason);
}

public interface IAgreementService
{
    Task<AgreementResponse> CreateAgreementAsync(string bookingId, string userId);
    Task<List<AgreementResponse>> GetUserAgreementsAsync(string userId);
    Task<AgreementResponse?> GetAgreementAsync(string agreementId, string userId);
    Task SignAgreementAsync(string agreementId, string userId);
    Task<(byte[], string)> DownloadAgreementPdfAsync(string agreementId, string userId);
}

public interface ICaretakerService
{
    Task<List<CaretakerResponse>> GetAvailableCaretakersAsync(string? serviceType, string? area);
    Task<CaretakerResponse?> GetCaretakerProfileAsync(string caretakerId);
    Task AssignCaretakerToPropertyAsync(string propertyId, string caretakerId, string landlordId);
    Task<ServiceRequestResponse> CreateServiceRequestAsync(CreateServiceRequestRequest request, string userId);
    Task<List<ServiceRequestResponse>> GetServiceRequestsAsync(string userId);
    Task AcceptServiceRequestAsync(string requestId, string caretakerId);
    Task UpdateServiceRequestStatusAsync(string requestId, string status, string userId);
    Task SubmitServiceReviewAsync(string requestId, string userId, int rating, string? comment);
}

public interface IMaintenanceService
{
    Task<MaintenanceResponse> ReportMaintenanceAsync(CreateMaintenanceRequest request, string tenantId);
    Task<List<MaintenanceResponse>> GetPropertyMaintenanceAsync(string propertyId, string landlordId);
    Task<List<MaintenanceResponse>> GetTenantMaintenanceAsync(string tenantId);
    Task UpdateMaintenanceStatusAsync(string maintenanceId, string status, string userId, bool isAdmin);
    Task<ServiceRequestResponse> ConvertToServiceRequestAsync(string maintenanceId, string? caretakerId, string landlordId);
}

public interface IAgentService
{
    Task<List<AgentResponse>> GetVerifiedAgentsAsync(string? serviceArea);
    Task<AgentResponse?> GetAgentProfileAsync(string agentId);
    Task<ViewingRequestResponse> CreateViewingRequestAsync(string agentId, string propertyId, DateTime scheduledAt, string tenantId, string? notes);
    Task UpdateViewingRequestStatusAsync(string requestId, string status, string userId);
    // Viewing requests the caller is party to — as the requesting tenant and/or the assigned agent.
    Task<List<ViewingRequestResponse>> GetMyViewingRequestsAsync(string userId);
}

public interface IReviewService
{
    Task<ReviewResponse> CreateReviewAsync(string bookingId, string propertyId, string reviewerId, int rating, string? comment);
    Task<PagedResult<ReviewResponse>> GetPropertyReviewsAsync(string propertyId, int page, int pageSize);
    Task<List<ReviewResponse>> GetUserReviewsAsync(string userId);
    Task<ReviewResponse?> GetReviewAsync(string reviewId);
    Task DeleteReviewAsync(string reviewId, string userId);
}

public interface INotificationService
{
    /// <summary>Creates and persists a notification for a user (e.g. verification outcome / retry prompt).</summary>
    Task CreateAsync(string userId, string title, string message, string? relatedEntityId = null, string? relatedEntityType = null);

    /// <summary>
    /// Central notification dispatch. Always records an in-app notification, then sends SMS/email
    /// per the user's CommunicationPreference — UNLESS <paramref name="isEmergency"/> is true, in
    /// which case both channels are sent regardless of the opt-out and the record is flagged as an
    /// emergency override. Sender failures are logged, never thrown.
    /// </summary>
    Task NotifyAsync(string userId, NotificationType type, string title, string body, bool isEmergency = false);

    /// <summary>Returns the user's communication preference, creating an all-enabled default if none exists.</summary>
    Task<CommunicationPreferenceResponse> GetPreferenceAsync(string userId);

    /// <summary>Updates the user's SMS/email opt-out preference.</summary>
    Task<CommunicationPreferenceResponse> UpdatePreferenceAsync(string userId, bool smsEnabled, bool emailEnabled);

    Task<PagedResult<NotificationResponse>> GetUserNotificationsAsync(string userId, int page, int pageSize);
    Task MarkAsReadAsync(string notificationId, string userId);
    Task MarkAllAsReadAsync(string userId);
    Task<int> GetUnreadCountAsync(string userId);
    Task DeleteNotificationAsync(string notificationId, string userId);
}

public interface IReceiptService
{
    Task<PagedResult<ReceiptResponse>> GetUserReceiptsAsync(string userId, int page, int pageSize);
    Task<ReceiptResponse?> GetReceiptAsync(string receiptId, string userId);
    Task<(byte[], string)> DownloadReceiptPdfAsync(string receiptId, string userId);
    Task<ReceiptResponse?> GetReceiptByBookingAsync(string bookingId, string userId);
}

public interface IChatService
{
    Task<List<ConversationResponse>> GetUserConversationsAsync(string userId);
    Task<ConversationResponse> StartConversationAsync(string userId, string otherUserId, string? propertyId);
    Task<ConversationResponse?> GetConversationAsync(string conversationId, string userId);
    Task<PagedResult<MessageResponse>> GetConversationMessagesAsync(string conversationId, string userId, int page, int pageSize);
    Task<MessageResponse> SendMessageAsync(string conversationId, string userId, string body);
    Task MarkMessageAsReadAsync(string messageId, string userId);
    Task MarkConversationAsReadAsync(string conversationId, string userId);
    Task DeleteConversationAsync(string conversationId, string userId);
}
