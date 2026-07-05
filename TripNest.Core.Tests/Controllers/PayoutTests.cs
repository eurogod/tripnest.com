using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Payouts;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for the Paystack Transfers payout flow: account registration (masked, provider-
/// validated), the payout created when an escrow is released (net of the platform fee), the
/// no-account → retry path, ownership on retry, and the transfer webhook transitions.
/// Runs against a host with a configured webhook secret so signed transfer events verify.
/// </summary>
public class PayoutTests : TestBase
{
    private const string WebhookSecret = "sk_test_payout_webhook_secret";

    private readonly WebApplicationFactory<Program> _factory;

    public PayoutTests()
    {
        // Same fixture (same in-memory database); only the webhook signing secret is added.
        // IPaymentGateway is still the recording stub, so no real Paystack calls happen.
        _factory = _fixture.WithWebHostBuilder(builder =>
            builder.UseSetting("PaystackSettings:SecretKey", WebhookSecret));
        _httpClient = _factory.CreateClient();
    }

    private StubPaymentGateway Gateway => _factory.Services.GetRequiredService<StubPaymentGateway>();

    private static UpsertPayoutAccountRequest MomoAccount() => new()
    {
        Channel = "mobile_money",
        ProviderCode = "MTN",
        AccountNumber = "0551234567",
        AccountName = "Kwame Asante"
    };

    [Fact]
    public async Task UpsertAccount_RegistersWithProvider_AndMasksNumber()
    {
        await RegisterAndLoginAsync(UserRole.Landlord);

        var response = await _httpClient.PutAsJsonAsync("/api/payouts/account", MomoAccount());
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected OK but got {response.StatusCode}: {body}");
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");
        Assert.True(data.GetProperty("providerRegistered").GetBoolean());
        var masked = data.GetProperty("accountNumber").GetString()!;
        Assert.EndsWith("567", masked);
        Assert.DoesNotContain("0551234", masked);
    }

    [Fact]
    public async Task UpsertAccount_ProviderRejects_ReturnsBadRequest_AndSavesNothing()
    {
        await RegisterAndLoginAsync(UserRole.Landlord);
        Gateway.RecipientSucceeds = false;

        var response = await _httpClient.PutAsJsonAsync("/api/payouts/account", MomoAccount());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var account = await _httpClient.GetAsync("/api/payouts/account");
        Assert.Equal(HttpStatusCode.NotFound, account.StatusCode);
    }

    [Fact]
    public async Task ReleaseEscrow_WithAccount_TransfersNetOfFee()
    {
        var (tenantId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);

        // Landlord registers a payout destination first.
        await _httpClient.PutAsJsonAsync("/api/payouts/account", MomoAccount());

        var escrowId = await SeedHeldEscrowAsync(tenantId, landlordId, 1000m);

        // Landlord releases the escrow (client is authenticated as the landlord).
        var release = await _httpClient.PostAsync($"/api/escrow/{escrowId}/release", null);
        var releaseBody = await release.Content.ReadAsStringAsync();
        Assert.True(release.StatusCode == HttpStatusCode.OK, $"Expected OK but got {release.StatusCode}: {releaseBody}");

        // 10% platform fee (Platform:ManagementFeePercent) → GH₵900 actually moves.
        var transfer = Assert.Single(Gateway.Transfers);
        Assert.Equal(900m, transfer.Amount);

        var mine = await _httpClient.GetAsync("/api/payouts/mine");
        var data = JsonDocument.Parse(await mine.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.Equal(1, data.GetArrayLength());
        Assert.Equal(1000m, data[0].GetProperty("grossAmount").GetDecimal());
        Assert.Equal(100m, data[0].GetProperty("feeAmount").GetDecimal());
        Assert.Equal(900m, data[0].GetProperty("amount").GetDecimal());
        Assert.Equal((int)PayoutStatus.Paid, data[0].GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task ReleaseEscrow_WithoutAccount_PayoutWaits_ThenRetrySucceeds()
    {
        var (tenantId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        var escrowId = await SeedHeldEscrowAsync(tenantId, landlordId, 500m);

        // Release with no payout account: the release must still succeed; the payout waits.
        var release = await _httpClient.PostAsync($"/api/escrow/{escrowId}/release", null);
        Assert.Equal(HttpStatusCode.OK, release.StatusCode);
        Assert.Empty(Gateway.Transfers);

        var mine = await _httpClient.GetAsync("/api/payouts/mine");
        var data = JsonDocument.Parse(await mine.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.Equal((int)PayoutStatus.Pending, data[0].GetProperty("status").GetInt32());
        var payoutId = data[0].GetProperty("payoutId").GetString()!;

        // Retrying before adding an account stays a clean 400.
        var early = await _httpClient.PostAsync($"/api/payouts/{payoutId}/retry", null);
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);

        // Add the account, retry — money moves.
        await _httpClient.PutAsJsonAsync("/api/payouts/account", MomoAccount());
        var retry = await _httpClient.PostAsync($"/api/payouts/{payoutId}/retry", null);
        var retryBody = await retry.Content.ReadAsStringAsync();
        Assert.True(retry.StatusCode == HttpStatusCode.OK, $"Expected OK but got {retry.StatusCode}: {retryBody}");

        var transfer = Assert.Single(Gateway.Transfers);
        Assert.Equal(450m, transfer.Amount); // 500 minus 10% fee
    }

    [Fact]
    public async Task TransferWebhook_FailedThenSuccess_DrivesPayoutStatus()
    {
        var (tenantId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await _httpClient.PutAsJsonAsync("/api/payouts/account", MomoAccount());

        // Transfer is accepted but settles asynchronously — the webhook decides the outcome.
        Gateway.TransferStatus = "pending";
        var escrowId = await SeedHeldEscrowAsync(tenantId, landlordId, 200m);
        await _httpClient.PostAsync($"/api/escrow/{escrowId}/release", null);

        var payoutId = GetSinglePayoutId();
        Assert.Equal(PayoutStatus.Processing, GetPayoutStatus(payoutId));

        await PostSignedWebhookAsync("transfer.failed", payoutId, reason: "Insufficient balance");
        Assert.Equal(PayoutStatus.Failed, GetPayoutStatus(payoutId));

        await PostSignedWebhookAsync("transfer.success", payoutId);
        Assert.Equal(PayoutStatus.Paid, GetPayoutStatus(payoutId));
    }

    [Fact]
    public async Task Retry_AnotherLandlordsPayout_IsForbidden()
    {
        var (tenantId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        var escrowId = await SeedHeldEscrowAsync(tenantId, landlordId, 300m);
        await _httpClient.PostAsync($"/api/escrow/{escrowId}/release", null);
        var payoutId = GetSinglePayoutId();

        await RegisterAndLoginAsync(UserRole.Landlord); // a different landlord
        var response = await _httpClient.PostAsync($"/api/payouts/{payoutId}/retry", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Seeds a property (owned by landlordId), a checked-out booking, and a held escrow.</summary>
    private async Task<string> SeedHeldEscrowAsync(string tenantId, string landlordId, decimal amount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = new Property
        {
            UserId = landlordId,
            Title = "Payout Test Property",
            Description = "d",
            Location = "Accra",
            Latitude = 5.6,
            Longitude = -0.2,
            Bedrooms = 1,
            Bathrooms = 1,
            MonthlyRent = 900m,
            DailyRate = 50m,
            PropertyType = "Apartment"
        };
        db.Set<Property>().Add(property);

        var booking = new Booking
        {
            TenantId = tenantId,
            PropertyId = property.Id,
            TotalAmount = amount,
            Status = BookingStatus.Confirmed,
            CheckInDate = DateTime.UtcNow.AddDays(-5),
            CheckOutDate = DateTime.UtcNow.AddDays(-2)
        };
        db.Set<Booking>().Add(booking);

        var escrow = new Escrow
        {
            BookingId = booking.Id,
            Amount = amount,
            Status = EscrowStatus.HeldInEscrow,
            PaymentReference = $"REF-{Guid.NewGuid():N}",
            HeldAt = DateTime.UtcNow.AddDays(-5)
        };
        db.Set<Escrow>().Add(escrow);

        await db.SaveChangesAsync();
        return escrow.Id;
    }

    private string GetSinglePayoutId()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.Set<Payout>().Single().Id;
    }

    private PayoutStatus GetPayoutStatus(string payoutId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.Set<Payout>().Single(p => p.Id == payoutId).Status;
    }

    /// <summary>Posts a Paystack-style transfer webhook signed with the configured secret,
    /// exactly as Paystack would (HMAC-SHA512 over the raw body).</summary>
    private async Task PostSignedWebhookAsync(string eventType, string reference, string? reason = null)
    {
        var payload = JsonSerializer.Serialize(new
        {
            @event = eventType,
            data = new { reference, reason }
        });

        var signature = Convert.ToHexString(
            HMACSHA512.HashData(Encoding.UTF8.GetBytes(WebhookSecret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/escrow/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", signature);

        var response = await _httpClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
