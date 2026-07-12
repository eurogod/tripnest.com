using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Services;

public interface IDynamicPricingService
{
    /// <summary>
    /// Returns pricing with demand-adjusted nightly rates for the stay window when the listing
    /// opted in to dynamic pricing — otherwise the input is returned untouched. Never mutates the
    /// stored settings; callers feed the result straight into the quote calculator so the number
    /// shown, searched, and charged is always the same.
    /// </summary>
    Task<PricingSettings?> AdjustAsync(Property property, PricingSettings? pricing, DateTime checkIn, DateTime checkOut);
}
