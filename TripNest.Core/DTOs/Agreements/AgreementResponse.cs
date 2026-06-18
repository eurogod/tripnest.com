using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Agreements;

public class AgreementResponse
{
    public required string AgreementId { get; set; }
    public required string BookingId { get; set; }
    public required string TermsContent { get; set; }
    public AgreementStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? TenantSignature { get; set; }
    public string? LandlordSignature { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
