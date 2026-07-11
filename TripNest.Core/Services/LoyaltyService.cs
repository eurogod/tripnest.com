using TripNest.Core.DTOs.Loyalty;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Platform loyalty tiers, computed live from completed stays (no stored state to drift).
/// The discount is platform-funded and applied to the stay subtotal at quote/booking time —
/// hosts receive their normal rate.
/// </summary>
public class LoyaltyService : ILoyaltyService
{
    private readonly IBookingRepository _bookingRepository;

    // (minimum completed stays, tier name, stay discount %) — highest threshold first.
    private static readonly (int MinStays, string Tier, decimal DiscountPercent)[] Tiers =
    {
        (10, "Platinum", 8m),
        (6, "Gold", 5m),
        (3, "Silver", 3m),
        (0, "Bronze", 0m),
    };

    public LoyaltyService(IBookingRepository bookingRepository)
    {
        _bookingRepository = bookingRepository;
    }

    public async Task<LoyaltyStatusResponse> GetStatusAsync(string userId)
    {
        var completedStays = (await _bookingRepository.FindAsync(b =>
                b.TenantId == userId &&
                (b.Status == BookingStatus.CheckedOut || b.Status == BookingStatus.Completed)))
            .Count();

        var current = Tiers.First(t => completedStays >= t.MinStays);
        var next = Tiers.LastOrDefault(t => t.MinStays > completedStays);

        return new LoyaltyStatusResponse
        {
            Tier = current.Tier,
            CompletedStays = completedStays,
            DiscountPercent = current.DiscountPercent,
            NextTier = next == default ? null : next.Tier,
            StaysToNextTier = next == default ? null : next.MinStays - completedStays
        };
    }

    public async Task<decimal> GetDiscountPercentAsync(string userId) =>
        (await GetStatusAsync(userId)).DiscountPercent;
}
