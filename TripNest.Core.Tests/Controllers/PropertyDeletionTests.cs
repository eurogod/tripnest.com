using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// DELETE/PUT /api/properties/{id}: only the owner (or admin) may remove or edit a listing;
/// a never-booked listing is hard-deleted, while one with booking history is archived instead
/// (escrow audit events are delete-restricted, so purging money history must be impossible).
/// </summary>
public class PropertyDeletionTests : TestBase
{
    [Fact]
    public async Task Delete_NeverBooked_HardDeletes()
    {
        var (_, _, propertyId) = await CreateListingAsync();

        var res = await _httpClient.DeleteAsync($"/api/properties/{propertyId}");
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.OK, $"Expected OK but got {res.StatusCode}: {body}");
        Assert.Contains("deleted", body);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null(await db.Set<Property>().FindAsync(propertyId));
    }

    [Fact]
    public async Task Delete_WithBookingHistory_ArchivesInsteadOfDeleting()
    {
        var (_, landlordToken, propertyId) = await CreateListingAsync();

        // A tenant books it — the listing now has money-path history.
        await RegisterAndLoginAsync(UserRole.Tenant);
        var checkIn = DateTime.UtcNow.Date.AddDays(10);
        var bookRes = await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = checkIn,
            checkOutDate = checkIn.AddDays(2),
            guests = 1
        });
        Assert.Equal(HttpStatusCode.Created, bookRes.StatusCode);

        // Owner deletes: the API reports archival, the row survives as Archived.
        AuthAs(landlordToken);
        var res = await _httpClient.DeleteAsync($"/api/properties/{propertyId}");
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.OK, $"Expected OK but got {res.StatusCode}: {body}");
        Assert.Contains("archived", body);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await db.Set<Property>().FindAsync(propertyId);
        Assert.NotNull(property);
        Assert.Equal(PropertyStatus.Archived, property!.Status);

        // Archived listings no longer surface in search.
        ClearAuth();
        var search = await _httpClient.GetAsync("/api/properties/search?location=Deletion");
        var items = JsonDocument.Parse(await search.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data");
        Assert.Equal(0, items.GetArrayLength());
    }

    [Fact]
    public async Task Delete_ByNonOwner_IsForbidden()
    {
        var (_, _, propertyId) = await CreateListingAsync();

        // A different (verified) landlord must not be able to delete it.
        var (otherId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(otherId);
        var res = await _httpClient.DeleteAsync($"/api/properties/{propertyId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Update_ByNonOwner_IsForbidden()
    {
        var (_, _, propertyId) = await CreateListingAsync();

        var (otherId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(otherId);
        var res = await _httpClient.PutAsJsonAsync($"/api/properties/{propertyId}", NewListingRequest());
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>Registers a verified landlord and creates a listing through the API.
    /// Leaves the client authenticated as that landlord.</summary>
    private async Task<(string LandlordId, string Token, string PropertyId)> CreateListingAsync()
    {
        var (landlordId, token) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(landlordId);

        var res = await _httpClient.PostAsJsonAsync("/api/properties", NewListingRequest());
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.Created, $"Expected Created but got {res.StatusCode}: {body}");
        var propertyId = JsonDocument.Parse(body).RootElement
            .GetProperty("data").GetProperty("propertyId").GetString()!;
        return (landlordId, token, propertyId);
    }

    private static object NewListingRequest() => new
    {
        title = "Deletion Test Listing",
        description = "d",
        location = "Deletion Test Town",
        latitude = 5.6,
        longitude = -0.2,
        bedrooms = 2,
        bathrooms = 1,
        monthlyRent = 1200m,
        dailyRate = 50m,
        propertyType = "Apartment"
    };

    private void AuthAs(string token) =>
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
