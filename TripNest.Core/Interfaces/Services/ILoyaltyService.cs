using TripNest.Core.DTOs.Loyalty;

namespace TripNest.Core.Interfaces.Services;

public interface ILoyaltyService
{
    Task<LoyaltyStatusResponse> GetStatusAsync(string userId);
    /// <summary>The caller's active stay-discount percent (0 for new guests).</summary>
    Task<decimal> GetDiscountPercentAsync(string userId);
}
