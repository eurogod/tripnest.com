using TripNest.Core.DTOs.Dashboard;

namespace TripNest.Core.Interfaces.Services;

/// <summary>Aggregated platform-wide statistics for the admin dashboard.</summary>
public interface IDashboardStatsService
{
    Task<DashboardStatsResponse> GetStatsAsync();
}
