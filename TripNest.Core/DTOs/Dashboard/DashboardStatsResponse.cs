namespace TripNest.Core.DTOs.Dashboard;

public class DashboardStatsResponse
{
    public int TotalUsers { get; set; }
    public int TotalTenants { get; set; }
    public int TotalLandlords { get; set; }
    public int TotalAgents { get; set; }
    public int TotalCaretakers { get; set; }
    public int VerifiedUsers { get; set; }
    public int PendingVerifications { get; set; }
    public int TotalProperties { get; set; }
    public int ActiveProperties { get; set; }
    public int PendingWalkthroughs { get; set; }
    public int TotalBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public decimal TotalEscrowHeld { get; set; }
    public decimal TotalEscrowReleased { get; set; }
    public int OpenDisputes { get; set; }
    public int OpenMaintenanceRequests { get; set; }
    public int ActiveServiceRequests { get; set; }
    public decimal AverageTrustScore { get; set; }
}
