using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TripNest.Core.Services;

namespace TripNest.Core.Tests;

/// <summary>
/// The real Paystack gateway must refuse to exist without a secret key (Program.cs registers
/// SimulatedPaymentGateway instead, Development only), and the simulator must cover the same
/// surface so dev/CI flows work: initiate returns a simulated reference + checkout URL, verify
/// reports Simulated=true, and refund succeeds.
/// </summary>
public class PaystackGatewayTests
{
    [Fact]
    public void PaystackGateway_WithoutKey_Throws()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        Assert.Throws<InvalidOperationException>(() =>
            new PaystackPaymentGateway(new HttpClient(), config, NullLogger<PaystackPaymentGateway>.Instance));
    }

    private static SimulatedPaymentGateway BuildSimulated() =>
        new(NullLogger<SimulatedPaymentGateway>.Instance);

    [Fact]
    public async Task SimulatedInitiatePayment_ReturnsSimulatedReference()
    {
        var result = await BuildSimulated().InitiatePaymentAsync(500m, "GHS", "tenant@example.com", "booking123");

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Reference));
        Assert.Contains("simulated", result.CheckoutUrl);
    }

    [Fact]
    public async Task SimulatedVerify_ReportsSimulated()
    {
        var result = await BuildSimulated().VerifyPaymentAsync("ref123");

        Assert.True(result.Success);
        Assert.True(result.Simulated);
    }

    [Fact]
    public async Task SimulatedRefund_ReturnsTrue()
    {
        Assert.True(await BuildSimulated().RefundAsync("ref123", 100m));
    }
}
