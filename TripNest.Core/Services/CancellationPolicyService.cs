using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Computes the tiered cancellation refund based on the property's CancellationPolicy and how
/// far out check-in is. See <see cref="CancellationPolicy"/> for the tier definitions.
/// </summary>
public class CancellationPolicyService : ICancellationPolicyService
{
    private readonly IBookingRepository _bookingRepository;

    public CancellationPolicyService(IBookingRepository bookingRepository)
    {
        _bookingRepository = bookingRepository;
    }

    public async Task<decimal> CalculateRefundPercentage(string bookingId)
    {
        var (policy, days, _) = await LoadAsync(bookingId);
        return RefundFor(policy, days);
    }

    public async Task<RefundPreview> PreviewAsync(string bookingId, string userId)
    {
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId)
            ?? throw new NotFoundException("Booking");

        // Only the booking's tenant or the property's landlord may see its refund figures —
        // otherwise any authenticated user could read another booking's amount, check-in date,
        // and policy by enumerating ids.
        var landlordId = booking.Property?.UserId;
        if (booking.TenantId != userId && landlordId != userId)
            throw new ForbiddenException("You do not have access to this booking");

        var policy = booking.Property?.CancellationPolicy ?? CancellationPolicy.Moderate;
        var days = (booking.CheckInDate - DateTime.UtcNow).TotalDays;
        var pct = RefundFor(policy, days);
        return new RefundPreview(pct, Math.Round(booking.TotalAmount * pct / 100m, 2), policy.ToString(), Math.Round(days, 2));
    }

    private async Task<(CancellationPolicy Policy, double DaysUntilCheckIn, decimal Amount)> LoadAsync(string bookingId)
    {
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId)
            ?? throw new NotFoundException("Booking");
        var policy = booking.Property?.CancellationPolicy ?? CancellationPolicy.Moderate;
        var days = (booking.CheckInDate - DateTime.UtcNow).TotalDays;
        return (policy, days, booking.TotalAmount);
    }

    private static decimal RefundFor(CancellationPolicy policy, double daysUntilCheckIn) => policy switch
    {
        CancellationPolicy.Flexible => daysUntilCheckIn >= 1 ? 100m : 0m,
        CancellationPolicy.Moderate => daysUntilCheckIn >= 5 ? 100m : daysUntilCheckIn >= 1 ? 50m : 0m,
        CancellationPolicy.Strict => daysUntilCheckIn >= 7 ? 50m : 0m,
        _ => 0m
    };
}
