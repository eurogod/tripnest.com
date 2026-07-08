namespace TripNest.Core.Enums;

public enum ViewingRequestStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Completed,
    /// <summary>Turned down by the agent before confirmation.</summary>
    Declined
}
