namespace TripNest.Core.DTOs.Escrow;

public class InitiateEscrowRequest
{
    public required string BookingId { get; set; }
}

public class WebhookCallbackRequest
{
    public required string BookingId { get; set; }
    public required string Reference { get; set; }
}

public class DisputeRequest
{
    public required string Reason { get; set; }
}

public class ResolveDisputeRequest
{
    public bool Approved { get; set; }
}

public class RefundRequest
{
    public required string Reason { get; set; }
}
