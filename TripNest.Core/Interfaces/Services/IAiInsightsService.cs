using TripNest.Core.DTOs.Ai;

namespace TripNest.Core.Interfaces.Services;

/// <summary>
/// The AI-assist surface, built on <see cref="IAiClient"/>. Everything here is ADVISORY —
/// summaries, explanations, coaching — and never drives money movement, verification outcomes,
/// or approvals. Each method throws a friendly 400 when no AI provider is configured, and
/// treats unparseable model output as a provider failure.
/// </summary>
public interface IAiInsightsService
{
    /// <summary>"What guests say" for a listing (cached; needs at least 2 reviews).</summary>
    Task<ReviewSummaryResponse> GetReviewSummaryAsync(string propertyId);

    /// <summary>Admin reading aid for a damage claim: both sides + photo evidence described.</summary>
    Task<AdminBriefResponse> GetClaimBriefAsync(string claimId);

    /// <summary>Admin reading aid for a disputed escrow: the dispute reason + audit trail.</summary>
    Task<AdminBriefResponse> GetDisputeBriefAsync(string escrowId);

    /// <summary>"2BR near UG under 800 in September" → structured criteria → search results.</summary>
    Task<NaturalSearchResponse> SearchNaturalAsync(string query);

    /// <summary>Plain-language agreement explanation in the caller's preferred language (parties only).</summary>
    Task<AgreementSummaryResponse> GetAgreementSummaryAsync(string agreementId, string userId);

    /// <summary>Owner-only listing coaching: deterministic completeness checks + AI suggestions + photo notes.</summary>
    Task<ListingQualityResponse> GetQualityReportAsync(string propertyId, string userId);

    /// <summary>Why the caller and another visible roommate profile fit (or what to discuss).</summary>
    Task<RoommateExplanationResponse> ExplainRoommateMatchAsync(string userId, string otherUserId);

    /// <summary>Reviewer assist: do the listing photos look consistent with the listing facts?</summary>
    Task<WalkthroughAiCheckResponse> GetWalkthroughCheckAsync(string propertyId, string reviewerId);

    /// <summary>Best-effort maintenance triage (urgency + trade category); null parts on failure.</summary>
    Task<(string? Urgency, string? Category)> TriageMaintenanceAsync(string description);
}
