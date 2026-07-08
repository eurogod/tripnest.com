namespace TripNest.Core.Enums;

public enum ServiceRequestStatus
{
    Pending,
    Accepted,
    InProgress,
    Completed,
    Cancelled,
    /// <summary>Turned down by the assigned caretaker before acceptance.</summary>
    Declined
}
