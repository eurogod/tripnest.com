using TripNest.Core.DTOs.Properties;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

/// <summary>
/// Single source of truth for what a stay costs. Search results, the quote endpoint, and booking
/// creation all price through here so the number shown up front is the number charged — never a
/// surprise fee on the last screen. Without <see cref="PricingSettings"/> it reproduces the
/// original flat calculation (daily rate, or monthly rent pro-rated), so listings that never
/// configured pricing behave exactly as before.
/// </summary>
public static class StayPricingCalculator
{
    /// <summary>Pro-rating divisor for listings priced by monthly rent only.</summary>
    public const int ProRataDaysPerMonth = 30;

    public static StayQuote Quote(
        Property property,
        PricingSettings? pricing,
        DateTime checkIn,
        DateTime checkOut,
        decimal loyaltyDiscountPercent = 0m)
    {
        var nights = (checkOut.Date - checkIn.Date).Days;
        var fallbackRate = property.DailyRate ?? property.MonthlyRent / ProRataDaysPerMonth;
        var baseRate = pricing is { BaseRate: > 0 } ? pricing.BaseRate : fallbackRate;
        var weekendRate = pricing is { WeekendRate: > 0 } ? pricing.WeekendRate : baseRate;

        decimal subtotal = 0m;
        for (var night = checkIn.Date; night < checkOut.Date; night = night.AddDays(1))
            subtotal += night.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday ? weekendRate : baseRate;

        // Longest-applicable single discount (not stacked), matching how the pricing page pitches it.
        var discountPercent = 0m;
        if (pricing is not null)
        {
            if (nights >= 28 && pricing.MonthlyDiscountPercent > 0) discountPercent = pricing.MonthlyDiscountPercent;
            else if (nights >= 7 && pricing.WeeklyDiscountPercent > 0) discountPercent = pricing.WeeklyDiscountPercent;
        }
        var lengthDiscount = Math.Round(subtotal * discountPercent / 100m, 2);

        var cleaningFee = pricing?.CleaningFee ?? 0m;

        // Loyalty applies to the stay (after length discount), not the cleaning fee — the fee
        // covers a real cost the platform shouldn't discount away from the host.
        var discountedStay = subtotal - lengthDiscount;
        var loyaltyDiscount = Math.Round(discountedStay * loyaltyDiscountPercent / 100m, 2);

        return new StayQuote
        {
            CheckIn = checkIn.Date,
            CheckOut = checkOut.Date,
            Nights = nights,
            StaySubtotal = Math.Round(subtotal, 2),
            CleaningFee = cleaningFee,
            LengthOfStayDiscount = lengthDiscount,
            LoyaltyDiscount = loyaltyDiscount,
            Total = Math.Round(discountedStay - loyaltyDiscount + cleaningFee, 2)
        };
    }
}
