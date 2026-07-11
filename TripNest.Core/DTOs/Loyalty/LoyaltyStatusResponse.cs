namespace TripNest.Core.DTOs.Loyalty;

public class LoyaltyStatusResponse
{
    public required string Tier { get; set; }
    public int CompletedStays { get; set; }
    /// <summary>Platform-funded discount applied to the stay subtotal of every booking.</summary>
    public decimal DiscountPercent { get; set; }
    public string? NextTier { get; set; }
    public int? StaysToNextTier { get; set; }
}
