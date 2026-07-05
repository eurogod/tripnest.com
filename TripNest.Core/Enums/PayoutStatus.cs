namespace TripNest.Core.Enums;

public enum PayoutStatus
{
    /// <summary>Created but not yet sent to the provider (e.g. the host has no payout account yet).</summary>
    Pending,

    /// <summary>Transfer initiated with the provider; awaiting its success/failure webhook.</summary>
    Processing,

    /// <summary>The provider confirmed the money reached the host.</summary>
    Paid,

    /// <summary>The provider rejected or reversed the transfer; retryable.</summary>
    Failed
}
