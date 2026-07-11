using TripNest.Core.Enums;

namespace TripNest.Core.Models;

public class Agreement
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string BookingId { get; set; }
    public Booking? Booking { get; set; }
    public required string TermsContent { get; set; }
    public decimal RentAmount { get; set; }
    public RentFrequency RentFrequency { get; set; } = RentFrequency.Monthly;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public AgreementStatus Status { get; set; } = AgreementStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SignedAt { get; set; }
    public string? TenantSignature { get; set; }
    public string? LandlordSignature { get; set; }
    /// <summary>Snapshot of each party's profile signature image at the moment THEY signed —
    /// executed contracts keep showing the signature as it was, even if the profile one changes.</summary>
    public string? TenantSignatureImagePath { get; set; }
    public string? LandlordSignatureImagePath { get; set; }
    /// <summary>SHA-256 (hex) of <see cref="TermsContent"/> captured at the first signature. The
    /// second signature refuses to bind if the text no longer hashes to this — tamper evidence.</summary>
    public string? TermsHash { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
