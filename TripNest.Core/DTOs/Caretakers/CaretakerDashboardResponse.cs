namespace TripNest.Core.DTOs.Caretakers;

/// <summary>
/// Real service-request and engagement metrics for the signed-in caretaker, aggregated across
/// every caretaker profile the user owns.
/// </summary>
public class CaretakerDashboardResponse
{
    public int TotalServiceRequests { get; set; }
    public int PendingRequests { get; set; }
    public int ActiveServiceRequests { get; set; }
    public int CompletedServiceRequests { get; set; }
    public int CompletedThisMonth { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    /// <summary>Sum of the contracted monthly compensation across the caretaker's active engagements.</summary>
    public decimal MonthlyCompensation { get; set; }
    public int ActiveEngagements { get; set; }
    public List<ServiceRequestResponse> RecentRequests { get; set; } = new();
}
