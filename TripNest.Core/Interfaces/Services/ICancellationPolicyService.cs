namespace TripNest.Core.Interfaces.Services;

public record RefundPreview(decimal RefundPercentage, decimal RefundAmount, string PolicyName, double DaysUntilCheckIn);

public interface ICancellationPolicyService
{
    /// <summary>Refund percentage (0-100) for cancelling a booking now, per its property's policy.</summary>
    Task<decimal> CalculateRefundPercentage(string bookingId);

    /// <summary>Full preview the tenant sees before confirming a cancellation.</summary>
    Task<RefundPreview> PreviewAsync(string bookingId);
}
