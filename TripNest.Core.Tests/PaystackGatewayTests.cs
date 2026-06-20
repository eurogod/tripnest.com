using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TripNest.Core.Services;

namespace TripNest.Core.Tests;

/// <summary>
/// Verifies the Paystack gateway degrades gracefully without a configured secret key, so dev/CI
/// flows work: initiate returns a simulated reference + checkout URL, and refund succeeds.
/// </summary>
public class PaystackGatewayTests
{
    private static PaystackPaymentGateway BuildUnconfigured()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        return new PaystackPaymentGateway(new HttpClient(), config, NullLogger<PaystackPaymentGateway>.Instance);
    }

    [Fact]
    public async Task InitiatePayment_WithoutKey_ReturnsSimulatedReference()
    {
        var result = await BuildUnconfigured().InitiatePaymentAsync(500m, "GHS", "tenant@example.com", "booking123");

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Reference));
        Assert.Contains("simulated", result.CheckoutUrl);
    }

    [Fact]
    public async Task Refund_WithoutKey_ReturnsTrue()
    {
        Assert.True(await BuildUnconfigured().RefundAsync("ref123", 100m));
    }
}
