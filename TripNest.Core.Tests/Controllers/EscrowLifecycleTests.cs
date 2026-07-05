using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Auth;
using TripNest.Core.DTOs.Escrow;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for the escrow dispute/refund state machine and cancellation-refund fairness:
/// disputes only from held funds, dispute-refunds must move money through the provider,
/// approving a dispute completes the stay, and a landlord-initiated cancellation always
/// refunds the tenant 100% regardless of the property's cancellation policy.
/// </summary>
public class EscrowLifecycleTests : TestBase
{
    private StubPaymentGateway Gateway => _fixture.Services.GetRequiredService<StubPaymentGateway>();

    [Fact]
    public async Task RaiseDispute_BeforeFundsAreHeld_IsRejected()
    {
        var (tenantId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        var (_, escrowId) = await SeedBookingWithEscrowAsync(tenantId, "landlord-d1", EscrowStatus.Pending, 100m);

        var response = await _httpClient.PostAsJsonAsync($"/api/escrow/{escrowId}/dispute",
            new DisputeRequest { Reason = "No funds yet" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResolveDispute_InTenantsFavour_RefundsThroughProvider()
    {
        var (tenantId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        var (_, escrowId) = await SeedBookingWithEscrowAsync(tenantId, "landlord-d2", EscrowStatus.Disputed, 250m, "REF-DISPUTE-250");
        await LoginAsAdminAsync();

        var response = await _httpClient.PatchAsJsonAsync($"/api/escrow/{escrowId}/resolve-dispute",
            new ResolveDisputeRequest { Approved = false });
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected OK but got {response.StatusCode}: {body}");

        // The money must actually move: the provider refund is issued for the full amount.
        Assert.Contains(Gateway.Refunds, r => r.Reference == "REF-DISPUTE-250" && r.Amount == 250m);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var escrow = await db.Set<Escrow>().FindAsync(escrowId);
        Assert.Equal(EscrowStatus.Refunded, escrow!.Status);
    }

    [Fact]
    public async Task ResolveDispute_InLandlordsFavour_ReleasesAndCompletesBooking()
    {
        var (tenantId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        var (bookingId, escrowId) = await SeedBookingWithEscrowAsync(tenantId, "landlord-d3", EscrowStatus.Disputed, 250m, "REF-DISPUTE-OK");
        await LoginAsAdminAsync();

        var response = await _httpClient.PatchAsJsonAsync($"/api/escrow/{escrowId}/resolve-dispute",
            new ResolveDisputeRequest { Approved = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var escrow = await db.Set<Escrow>().FindAsync(escrowId);
        var booking = await db.Set<Booking>().FindAsync(bookingId);
        Assert.Equal(EscrowStatus.Released, escrow!.Status);
        Assert.Equal(BookingStatus.Completed, booking!.Status);
        Assert.Empty(Gateway.Refunds);
    }

    [Fact]
    public async Task Cancel_ByLandlord_RefundsTenantInFull()
    {
        var (tenantId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);

        // Check-in is tomorrow: the Moderate policy would give a cancelling TENANT only 50%.
        var (bookingId, _) = await SeedBookingWithEscrowAsync(
            tenantId, landlordId, EscrowStatus.HeldInEscrow, 300m, "REF-HOST-CANCEL");

        // The client is authenticated as the landlord (last login) — the host cancels.
        var response = await _httpClient.PostAsync($"/api/bookings/{bookingId}/cancel", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected OK but got {response.StatusCode}: {body}");
        Assert.Contains(Gateway.Refunds, r => r.Reference == "REF-HOST-CANCEL" && r.Amount == 300m);
    }

    [Fact]
    public async Task Cancel_ByTenant_AppliesCancellationPolicy()
    {
        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        var (tenantId, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        // Check-in in 2 days under the default Moderate policy (1–5 days out) → the tenant gets
        // 50% back. (Seeding at exactly 1 day would race the clock below the 1-day threshold.)
        var (bookingId, _) = await SeedBookingWithEscrowAsync(
            tenantId, landlordId, EscrowStatus.HeldInEscrow, 200m, "REF-TENANT-CANCEL");

        // The client is authenticated as the tenant (last login) — the tenant cancels.
        var response = await _httpClient.PostAsync($"/api/bookings/{bookingId}/cancel", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected OK but got {response.StatusCode}: {body}");
        Assert.Contains(Gateway.Refunds, r => r.Reference == "REF-TENANT-CANCEL" && r.Amount == 100m);
    }

    /// <summary>Creates a property (owned by ownerId), a confirmed booking checking in 2 days out
    /// (comfortably inside the Moderate policy's 50% tier), and an escrow in the given status.
    /// Returns (bookingId, escrowId).</summary>
    private async Task<(string BookingId, string EscrowId)> SeedBookingWithEscrowAsync(
        string tenantId, string ownerId, EscrowStatus status, decimal amount, string? reference = null)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = new Property
        {
            UserId = ownerId,
            Title = "Escrow Lifecycle Test Property",
            Description = "d",
            Location = "Accra",
            Latitude = 5.6,
            Longitude = -0.2,
            Bedrooms = 1,
            Bathrooms = 1,
            MonthlyRent = 900m,
            DailyRate = 40m,
            PropertyType = "Apartment"
        };
        db.Set<Property>().Add(property);

        var booking = new Booking
        {
            TenantId = tenantId,
            PropertyId = property.Id,
            TotalAmount = amount,
            Status = BookingStatus.Confirmed,
            CheckInDate = DateTime.UtcNow.AddDays(2),
            CheckOutDate = DateTime.UtcNow.AddDays(5)
        };
        db.Set<Booking>().Add(booking);

        var escrow = new Escrow
        {
            BookingId = booking.Id,
            Amount = amount,
            Status = status,
            PaymentReference = reference,
            HeldAt = status is EscrowStatus.HeldInEscrow or EscrowStatus.Disputed ? DateTime.UtcNow : null
        };
        db.Set<Escrow>().Add(escrow);

        await db.SaveChangesAsync();
        return (booking.Id, escrow.Id);
    }

    /// <summary>Provisions a user, flips their role to Admin in the database, and logs in again so
    /// the bearer token carries the Admin role claim (admins cannot self-register).</summary>
    private async Task LoginAsAdminAsync()
    {
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant, email);

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(userId);
            user!.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        }

        var login = await _httpClient.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = "Password@123" });
        var token = JsonDocument.Parse(await login.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
