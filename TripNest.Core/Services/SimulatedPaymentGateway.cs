using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Dev-only payment gateway that pretends every operation succeeds, so local flows and tests run
/// without Paystack credentials. Registered instead of <see cref="PaystackPaymentGateway"/> when no
/// secret key is configured — Program.cs refuses to boot in that state outside Development, so the
/// real money path never carries a test-mode branch.
/// </summary>
public class SimulatedPaymentGateway : IPaymentGateway
{
    private readonly ILogger<SimulatedPaymentGateway> _logger;

    public SimulatedPaymentGateway(ILogger<SimulatedPaymentGateway> logger)
    {
        _logger = logger;
    }

    public Task<PaymentInitResult> InitiatePaymentAsync(decimal amount, string currency, string customerEmail, string bookingId, string? callbackUrl = null)
    {
        var reference = $"TN-{bookingId[..Math.Min(8, bookingId.Length)]}-{Guid.NewGuid():N}";
        _logger.LogInformation("[Simulated gateway] checkout for booking {BookingId}, ref {Reference}", bookingId, reference);
        return Task.FromResult(new PaymentInitResult(true, $"https://checkout.paystack.test/simulated/{reference}", reference));
    }

    public Task<PaymentVerifyResult> VerifyPaymentAsync(string reference)
    {
        _logger.LogInformation("[Simulated gateway] verify success for ref {Reference}", reference);
        // Simulated=true: the gateway can't know the real amount — callers substitute what they expect.
        return Task.FromResult(new PaymentVerifyResult(true, 0m, Simulated: true));
    }

    public Task<bool> RefundAsync(string reference, decimal amount)
    {
        _logger.LogInformation("[Simulated gateway] refund of {Amount} for ref {Reference}", amount, reference);
        return Task.FromResult(true);
    }

    public Task<TransferRecipientResult> CreateTransferRecipientAsync(
        string accountName, string accountNumber, string providerCode, string channel, string currency)
    {
        _logger.LogInformation("[Simulated gateway] transfer recipient for {AccountName}", accountName);
        return Task.FromResult(new TransferRecipientResult(true, $"RCP_SIM_{Guid.NewGuid():N}", null));
    }

    public Task<TransferResult> InitiateTransferAsync(
        decimal amount, string currency, string recipientCode, string reference, string reason)
    {
        _logger.LogInformation("[Simulated gateway] transfer of {Amount} {Currency}, ref {Reference}", amount, currency, reference);
        return Task.FromResult(new TransferResult(true, $"TRF_SIM_{Guid.NewGuid():N}", "success", null));
    }
}
