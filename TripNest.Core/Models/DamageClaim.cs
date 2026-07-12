using TripNest.Core.Enums;

namespace TripNest.Core.Models;

/// <summary>
/// A host's damage-protection claim against a stay. Filed with photo evidence within the filing
/// window after checkout, visible to the tenant (who can attach one response), decided by an
/// admin. Approval creates the landlord's payout immediately through the standard transfer
/// machinery — the fast-payout promise is the whole point of the feature.
/// </summary>
public class DamageClaim
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string BookingId { get; set; }
    public Booking? Booking { get; set; }
    public required string LandlordId { get; set; }
    public required string TenantId { get; set; }

    /// <summary>What the host claims the damage cost.</summary>
    public decimal Amount { get; set; }
    /// <summary>What the admin actually approved (may be less than claimed).</summary>
    public decimal? ApprovedAmount { get; set; }
    public required string Description { get; set; }
    /// <summary>Comma-separated stored paths of the evidence photos.</summary>
    public string? PhotoPaths { get; set; }

    public DamageClaimStatus Status { get; set; } = DamageClaimStatus.Submitted;
    /// <summary>The tenant's side of the story (one response).</summary>
    public string? TenantResponse { get; set; }
    /// <summary>The admin's decision note (rejection reason / approval remark).</summary>
    public string? ResolutionNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
