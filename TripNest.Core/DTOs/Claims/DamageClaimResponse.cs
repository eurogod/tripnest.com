using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Claims;

public class DamageClaimResponse
{
    public required string ClaimId { get; set; }
    public required string BookingId { get; set; }
    public required string LandlordId { get; set; }
    public required string TenantId { get; set; }
    public decimal Amount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public required string Description { get; set; }
    public List<string> PhotoPaths { get; set; } = new();
    public DamageClaimStatus Status { get; set; }
    public string? TenantResponse { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
