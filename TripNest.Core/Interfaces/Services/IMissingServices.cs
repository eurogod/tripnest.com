using Microsoft.AspNetCore.Http;
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
using TripNest.Core.Models;

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
    Task<PagedResult<EscrowResponse>> GetMyEscrowsAsync(string userId, int page, int pageSize);
    Task ReleaseEscrowAsync(string escrowId, string userId);
    Task RaiseDisputeAsync(string escrowId, string userId, string reason);
    Task ResolveDisputeAsync(string escrowId, bool approved);
    Task RefundEscrowAsync(string escrowId, string reason);
}

public interface IAgreementService
{
    Task<AgreementResponse> CreateAgreementAsync(string bookingId, string userId);
    Task<PagedResult<AgreementResponse>> GetUserAgreementsAsync(string userId, int page, int pageSize);
    Task<AgreementResponse?> GetAgreementAsync(string agreementId, string userId);
    Task SignAgreementAsync(string agreementId, string userId);
    /// <summary>Marks a signed agreement Terminated (either party; record-keeping only).</summary>
    Task<AgreementResponse> TerminateAgreementAsync(string agreementId, string userId, string reason);
    Task<(byte[], string)> DownloadAgreementPdfAsync(string agreementId, string userId);
}

public interface ICaretakerService
{
    Task<PagedResult<CaretakerResponse>> GetAvailableCaretakersAsync(string? serviceType, string? area, int page, int pageSize);
    Task<CaretakerResponse?> GetCaretakerProfileAsync(string caretakerId);
    /// <summary>The caller's own directory profile, or null if they haven't created one yet.</summary>
    Task<CaretakerResponse?> GetMyProfileAsync(string userId);
    /// <summary>Creates (or updates) the caller's directory profile — the only way a Caretaker-role
    /// account becomes visible in the public caretakers list. A Suspended profile stays suspended.</summary>
    Task<CaretakerResponse> UpsertMyProfileAsync(string userId, UpsertCaretakerProfileRequest request);
    Task AssignCaretakerToPropertyAsync(string propertyId, string caretakerId, string landlordId);
    /// <summary>Ends the active assignment between the caretaker and the landlord's property.</summary>
    Task UnassignCaretakerFromPropertyAsync(string propertyId, string caretakerId, string landlordId);
    /// <summary>Assignments the caller is party to — on their properties and/or as the caretaker.</summary>
    Task<PagedResult<CaretakerAssignmentResponse>> GetMyAssignmentsAsync(string userId, int page, int pageSize);
    Task<ServiceRequestResponse> CreateServiceRequestAsync(CreateServiceRequestRequest request, string userId);
    Task<PagedResult<ServiceRequestResponse>> GetServiceRequestsAsync(string userId, int page, int pageSize);
    Task AcceptServiceRequestAsync(string requestId, string caretakerId);
    /// <summary>Caretaker turns down a pending request (Pending → Declined).</summary>
    Task DeclineServiceRequestAsync(string requestId, string caretakerId);
    Task UpdateServiceRequestStatusAsync(string requestId, string status, string userId);
    Task SubmitServiceReviewAsync(string requestId, string userId, int rating, string? comment);
}

public interface IMaintenanceService
{
    Task<MaintenanceResponse> ReportMaintenanceAsync(CreateMaintenanceRequest request, string tenantId);
    Task<PagedResult<MaintenanceResponse>> GetPropertyMaintenanceAsync(string propertyId, string landlordId, int page, int pageSize);
    Task<PagedResult<MaintenanceResponse>> GetTenantMaintenanceAsync(string tenantId, int page, int pageSize);
    Task UpdateMaintenanceStatusAsync(string maintenanceId, string status, string userId, bool isAdmin);
    Task<ServiceRequestResponse> ConvertToServiceRequestAsync(string maintenanceId, string? caretakerId, string landlordId);
}

public interface IAgentService
{
    Task<PagedResult<AgentResponse>> GetVerifiedAgentsAsync(string? serviceArea, int page, int pageSize);
    Task<AgentResponse?> GetAgentProfileAsync(string agentId);
    /// <summary>The caller's own directory profile, or null if they haven't created one yet.</summary>
    Task<AgentResponse?> GetMyProfileAsync(string userId);
    /// <summary>Creates (or updates) the caller's directory profile — the only way an Agent-role
    /// account becomes visible in the public agents list. A Suspended profile stays suspended.</summary>
    Task<AgentResponse> UpsertMyProfileAsync(string userId, UpsertAgentProfileRequest request);
    Task<ViewingRequestResponse> CreateViewingRequestAsync(string agentId, string propertyId, DateTime scheduledAt, string tenantId, string? notes);
    Task UpdateViewingRequestStatusAsync(string requestId, string status, string userId);
    /// <summary>Agent turns down a pending viewing request (Pending → Declined).</summary>
    Task DeclineViewingRequestAsync(string requestId, string userId);
    /// <summary>The requesting tenant reviews a completed viewing (rating 1–5).</summary>
    Task SubmitViewingReviewAsync(string requestId, string userId, int rating, string? comment);
    // Viewing requests the caller is party to — as the requesting tenant and/or the assigned agent.
    Task<PagedResult<ViewingRequestResponse>> GetMyViewingRequestsAsync(string userId, int page, int pageSize);
}

public interface IReviewService
{
    Task<ReviewResponse> CreateReviewAsync(string bookingId, string propertyId, string reviewerId, int rating, string? comment);
    Task<PagedResult<ReviewResponse>> GetPropertyReviewsAsync(string propertyId, int page, int pageSize);
    Task<PagedResult<ReviewResponse>> GetUserReviewsAsync(string userId, int page, int pageSize);
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

/// <summary>Watches chat for off-platform payment attempts; warns recipients, never blocks.</summary>
public interface IScamDetectionService
{
    Task ScanMessageAsync(Message message, Conversation conversation);
}

public interface IChatService
{
    Task<PagedResult<ConversationResponse>> GetUserConversationsAsync(string userId, int page, int pageSize);
    Task<ConversationResponse> StartConversationAsync(string userId, string otherUserId, string? propertyId);
    Task<ConversationResponse?> GetConversationAsync(string conversationId, string userId);
    Task<PagedResult<MessageResponse>> GetConversationMessagesAsync(string conversationId, string userId, int page, int pageSize);
    Task<MessageResponse> SendMessageAsync(string conversationId, string userId, string body);
    /// <summary>Sends an image, voice note or document attachment (kind inferred from the file).</summary>
    Task<MessageResponse> SendAttachmentAsync(string conversationId, string userId, IFormFile file, string? caption);
    Task MarkMessageAsReadAsync(string messageId, string userId);
    Task MarkConversationAsReadAsync(string conversationId, string userId);
    Task DeleteConversationAsync(string conversationId, string userId);
    Task<string> SuggestReplyAsync(string conversationId, string userId);
}
