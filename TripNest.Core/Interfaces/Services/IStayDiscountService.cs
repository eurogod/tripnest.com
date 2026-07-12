using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Services;

/// <summary>
/// The single owner of the guest stay-discount rule, consumed by BOTH the quote path and the
/// booking charge so the two can never drift apart: the guest's loyalty-tier discount, or — on
/// Student-stayType listings when they hold an active student verification — the student rate,
/// whichever is larger (never stacked).
/// </summary>
public interface IStayDiscountService
{
    Task<decimal> GetPercentAsync(string userId, Property property);
}
