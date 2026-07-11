using Microsoft.Extensions.Options;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Options;

namespace TripNest.Core.Services;

/// <summary>
/// Computes the tiered cancellation refund based on the property's CancellationPolicy and how
/// far out check-in is. See <see cref="CancellationPolicy"/> for the tier definitions.
///
/// One platform-wide guarantee sits above every listing policy: cancelling within the grace
/// window (<see cref="PlatformOptions.CancellationGraceHours"/> of booking, when check-in is
/// still at least <see cref="PlatformOptions.CancellationGraceMinDaysBeforeCheckIn"/> days away)
/// always refunds 100% — a uniform, guest-fair rule no host policy can override.
/// </summary>
public class CancellationPolicyService : ICancellationPolicyService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly PlatformOptions _platform;

    public CancellationPolicyService(IBookingRepository bookingRepository, IOptions<PlatformOptions> platformOptions)
    {
        _bookingRepository = bookingRepository;
        _platform = platformOptions.Value;
    }

    public async Task<decimal> CalculateRefundPercentage(string bookingId)
    {
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId)
            ?? throw new NotFoundException("Booking");
        return RefundPercentageFor(booking);
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
        var pct = RefundPercentageFor(booking);
        var policyName = InGracePeriod(booking) ? "GracePeriod" : policy.ToString();
        return new RefundPreview(pct, Math.Round(booking.TotalAmount * pct / 100m, 2), policyName, Math.Round(days, 2));
    }

    private decimal RefundPercentageFor(Models.Booking booking)
    {
        if (InGracePeriod(booking))
            return 100m;

        var policy = booking.Property?.CancellationPolicy ?? CancellationPolicy.Moderate;
        var days = (booking.CheckInDate - DateTime.UtcNow).TotalDays;
        return RefundFor(policy, days);
    }

    private bool InGracePeriod(Models.Booking booking)
    {
        if (_platform.CancellationGraceHours <= 0)
            return false;

        var hoursSinceBooked = (DateTime.UtcNow - booking.CreatedAt).TotalHours;
        var daysUntilCheckIn = (booking.CheckInDate - DateTime.UtcNow).TotalDays;
        return hoursSinceBooked <= _platform.CancellationGraceHours &&
               daysUntilCheckIn >= _platform.CancellationGraceMinDaysBeforeCheckIn;
    }

    private static decimal RefundFor(CancellationPolicy policy, double daysUntilCheckIn) => policy switch
    {
        CancellationPolicy.Flexible => daysUntilCheckIn >= 1 ? 100m : 0m,
        CancellationPolicy.Moderate => daysUntilCheckIn >= 5 ? 100m : daysUntilCheckIn >= 1 ? 50m : 0m,
        CancellationPolicy.Strict => daysUntilCheckIn >= 7 ? 50m : 0m,
        _ => 0m
    };
}
