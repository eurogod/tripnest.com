using Microsoft.EntityFrameworkCore;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Dashboard;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Builds the admin dashboard stats with a handful of GROUP BY queries instead of one COUNT
/// round-trip per figure (the previous shape was 13 sequential queries plus two full escrow
/// list loads summed in memory).
/// </summary>
public class DashboardStatsService : IDashboardStatsService
{
    private readonly AppDbContext _context;

    public DashboardStatsService(AppDbContext context) => _context = context;

    public async Task<DashboardStatsResponse> GetStatsAsync()
    {
        var usersByRole = await _context.Users
            .GroupBy(u => u.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync();
        var verifiedUsers = await _context.Users.CountAsync(u => u.IsVerified);

        var propertiesByStatus = await _context.Properties
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var bookingsByStatus = await _context.Bookings
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        // One aggregate per escrow status: count AND sum in the database, never the whole table.
        var escrowByStatus = await _context.Escrows
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Total = g.Sum(e => e.Amount) })
            .ToListAsync();

        int UsersIn(UserRole role) => usersByRole.FirstOrDefault(x => x.Role == role)?.Count ?? 0;
        int BookingsIn(BookingStatus status) => bookingsByStatus.FirstOrDefault(x => x.Status == status)?.Count ?? 0;
        decimal EscrowTotal(EscrowStatus status) => escrowByStatus.FirstOrDefault(x => x.Status == status)?.Total ?? 0m;

        return new DashboardStatsResponse
        {
            TotalUsers = usersByRole.Sum(x => x.Count),
            TotalTenants = UsersIn(UserRole.Tenant),
            TotalLandlords = UsersIn(UserRole.Landlord),
            TotalAgents = UsersIn(UserRole.Agent),
            TotalCaretakers = UsersIn(UserRole.Caretaker),
            VerifiedUsers = verifiedUsers,
            TotalProperties = propertiesByStatus.Sum(x => x.Count),
            ActiveProperties = propertiesByStatus.FirstOrDefault(x => x.Status == PropertyStatus.Active)?.Count ?? 0,
            TotalBookings = bookingsByStatus.Sum(x => x.Count),
            ConfirmedBookings = BookingsIn(BookingStatus.Confirmed),
            CompletedBookings = BookingsIn(BookingStatus.Completed),
            CancelledBookings = BookingsIn(BookingStatus.Cancelled),
            TotalEscrowHeld = EscrowTotal(EscrowStatus.HeldInEscrow),
            TotalEscrowReleased = EscrowTotal(EscrowStatus.Released),
            OpenDisputes = escrowByStatus.FirstOrDefault(x => x.Status == EscrowStatus.Disputed)?.Count ?? 0,
            AverageTrustScore = await ComputeAverageTrustScoreAsync()
        };
    }

    /// <summary>
    /// Platform-wide average trust score from the most recent daily snapshot run — a real figure,
    /// computed in the database. 0 when no snapshots exist yet (a brand-new install), which the
    /// UI should read as "no data" rather than a score.
    /// </summary>
    private async Task<decimal> ComputeAverageTrustScoreAsync()
    {
        var latestDate = await _context.TrustScoreSnapshots.MaxAsync(s => (DateOnly?)s.SnapshotDate);
        if (latestDate is null)
            return 0m;

        var average = await _context.TrustScoreSnapshots
            .Where(s => s.SnapshotDate == latestDate)
            .AverageAsync(s => s.FinalScore);
        return Math.Round(average, 1);
    }
}
