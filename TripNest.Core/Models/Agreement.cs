using TripNest.Core.Enums;

namespace TripNest.Core.Models;

public class Agreement
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string BookingId { get; set; }
    public Booking? Booking { get; set; }
    public required string TermsContent { get; set; }
    public AgreementStatus Status { get; set; } = AgreementStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SignedAt { get; set; }
    public string? TenantSignature { get; set; }
    public string? LandlordSignature { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
