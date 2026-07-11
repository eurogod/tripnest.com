using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Authorization guards on endpoints that take an entity id but must not act across owners:
/// walkthrough deletion (owner/admin only, scoped to the route's property) and the cancellation
/// refund preview (booking participants only). Both were IDORs — role-gated or auth-only but with
/// no per-record ownership check.
/// </summary>
public class OwnershipGuardTests : TestBase
{
    // --- Walkthrough delete ---------------------------------------------------------------

    [Fact]
    public async Task DeleteWalkthrough_ByNonOwnerLandlord_IsForbidden()
    {
        var (ownerId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        var (propertyId, walkthroughId) = await SeedPropertyWithWalkthroughAsync(ownerId);

        // A different verified landlord must not be able to delete it.
        var (otherId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(otherId);

        var res = await _httpClient.DeleteAsync($"/api/properties/{propertyId}/walkthroughs/{walkthroughId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        // And it still exists.
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.NotNull(await db.Set<Walkthrough>().FindAsync(walkthroughId));
    }

    [Fact]
    public async Task DeleteWalkthrough_WrongPropertyInRoute_IsNotFound()
    {
        var (ownerId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(ownerId);
        var (_, walkthroughId) = await SeedPropertyWithWalkthroughAsync(ownerId);

        // Owner is authenticated, but the walkthrough doesn't belong to this property id.
        var res = await _httpClient.DeleteAsync($"/api/properties/some-other-property/walkthroughs/{walkthroughId}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task DeleteWalkthrough_ByOwner_Succeeds()
    {
        var (ownerId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(ownerId);
        var (propertyId, walkthroughId) = await SeedPropertyWithWalkthroughAsync(ownerId);

        var res = await _httpClient.DeleteAsync($"/api/properties/{propertyId}/walkthroughs/{walkthroughId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null(await db.Set<Walkthrough>().FindAsync(walkthroughId));
    }

    // --- Cancellation preview -------------------------------------------------------------

    [Fact]
    public async Task CancellationPreview_ByNonParticipant_IsForbidden()
    {
        var bookingId = await SeedBookingAsync();

        // A logged-in user who is neither the tenant nor the landlord.
        await RegisterAndLoginAsync(UserRole.Tenant);
        var res = await _httpClient.GetAsync($"/api/bookings/{bookingId}/cancellation-preview");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task CancellationPreview_ByTenant_Succeeds()
    {
        var (tenantToken, bookingId) = await SeedBookingReturningTenantAsync();

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tenantToken);
        var res = await _httpClient.GetAsync($"/api/bookings/{bookingId}/cancellation-preview");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // --- Helpers --------------------------------------------------------------------------

    private async Task<(string PropertyId, string WalkthroughId)> SeedPropertyWithWalkthroughAsync(string ownerUserId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = new Property
        {
            UserId = ownerUserId,
            Title = "Guard Test Property",
            Description = "d",
            Location = "Accra",
            Latitude = 5.6,
            Longitude = -0.2,
            Bedrooms = 2,
            Bathrooms = 1,
            MonthlyRent = 1200m,
            DailyRate = 50m,
            PropertyType = "Apartment"
        };
        var walkthrough = new Walkthrough
        {
            PropertyId = property.Id,
            Title = "Tour",
            VideoPath = "uploads/walkthroughs/never-written.mp4"
        };
        db.Set<Property>().Add(property);
        db.Set<Walkthrough>().Add(walkthrough);
        await db.SaveChangesAsync();
        return (property.Id, walkthrough.Id);
    }

    private async Task<string> SeedBookingAsync() => (await SeedBookingReturningTenantAsync()).BookingId;

    /// <summary>Creates a property + a Pending booking through the API and returns the tenant's
    /// token alongside the booking id. Leaves the client authenticated as a fresh third party.</summary>
    private async Task<(string TenantToken, string BookingId)> SeedBookingReturningTenantAsync()
    {
        string propertyId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var property = new Property
            {
                UserId = "landlord-guard",
                Title = "Booking Guard Property",
                Description = "d",
                Location = "Accra",
                Latitude = 5.6,
                Longitude = -0.2,
                Bedrooms = 2,
                Bathrooms = 1,
                MonthlyRent = 1200m,
                DailyRate = 50m,
                PropertyType = "Apartment"
            };
            db.Set<Property>().Add(property);
            propertyId = property.Id;
            await db.SaveChangesAsync();
        }

        var (_, tenantToken) = await RegisterAndLoginAsync(UserRole.Tenant);
        var checkIn = DateTime.UtcNow.Date.AddDays(20);
        var bookRes = await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = checkIn,
            checkOutDate = checkIn.AddDays(3),
            guests = 1
        });
        var body = await bookRes.Content.ReadAsStringAsync();
        Assert.True(bookRes.StatusCode == HttpStatusCode.Created, $"Expected Created but got {bookRes.StatusCode}: {body}");
        var bookingId = JsonDocument.Parse(body).RootElement
            .GetProperty("data").GetProperty("bookingId").GetString()!;
        return (tenantToken, bookingId);
    }
}
