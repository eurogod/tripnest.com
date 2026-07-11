namespace TripNest.Core.DTOs.Properties;

/// <summary>
/// The true, no-surprises total for a stay: every component the guest will actually pay, computed
/// up front so search results and the booking screen always show the same number Paystack charges.
/// </summary>
public class StayQuote
{
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Nights { get; set; }
    /// <summary>Sum of nightly rates (weekend nights use the weekend rate when configured).</summary>
    public decimal StaySubtotal { get; set; }
    public decimal CleaningFee { get; set; }
    /// <summary>Length-of-stay discount from the listing's pricing rules (weekly/monthly), if any.</summary>
    public decimal LengthOfStayDiscount { get; set; }
    /// <summary>Loyalty-tier discount for the authenticated guest (0 when anonymous).</summary>
    public decimal LoyaltyDiscount { get; set; }
    public decimal Total { get; set; }
}
