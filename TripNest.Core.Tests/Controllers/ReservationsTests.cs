using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for the host reservations views: the list's guests count + derived stage, and the
/// reservation-details endpoint's earnings breakdown (nightly rate, management fee, owner payout)
/// and guest reviews — plus ownership enforcement.
/// </summary>
public class ReservationsTests : TestBase
{
    [Fact]
    public async Task Bookings_List_IncludesGuestsAndDerivedStage()
    {
        var (tenantId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);

        // Upcoming (future, confirmed), Complete (completed), Canceled — one of each.
        await SeedBookingAsync(tenantId, landlordId, BookingStatus.Confirmed, daysFromNow: 5, guests: 3);
        await SeedBookingAsync(tenantId, landlordId, BookingStatus.Completed, daysFromNow: -20, guests: 2);
        await SeedBookingAsync(tenantId, landlordId, BookingStatus.Cancelled, daysFromNow: 10, guests: 1);

        var response = await _httpClient.GetAsync("/api/landlord/bookings");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected OK but got {response.StatusCode}: {body}");
        var items = JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("items");

        var stages = new List<(int Guests, string Stage)>();
        foreach (var item in items.EnumerateArray())
            stages.Add((item.GetProperty("guests").GetInt32(), item.GetProperty("stage").GetString()!));

        Assert.Equal(3, stages.Count);
        Assert.Contains((3, "Upcoming"), stages);
        Assert.Contains((2, "Complete"), stages);
        Assert.Contains((1, "Canceled"), stages);
    }

    [Fact]
    public async Task ReservationDetails_ReturnsEarningsBreakdownAndGuestReview()
    {
        var (tenantId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);

        // 3 nights at ₵1266 total → nightly 422; default 20% fee → 253.20 fee, 1012.80 payout.
        var (bookingId, propertyId) = await SeedBookingAsync(
            tenantId, landlordId, BookingStatus.Completed, daysFromNow: -10, guests: 2, total: 1266m, nights: 3);

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<Review>().Add(new Review
            {
                ReviewerId = tenantId,
                RevieweeId = landlordId,
                PropertyId = propertyId,
                Rating = 5,
                Comment = "Wonderful stay",
                Type = ReviewType.Property
            });
            await db.SaveChangesAsync();
        }

        var response = await _httpClient.GetAsync($"/api/landlord/reservations/{bookingId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected OK but got {response.StatusCode}: {body}");
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");

        Assert.Equal(3, data.GetProperty("nights").GetInt32());
        Assert.Equal(2, data.GetProperty("guests").GetInt32());
        Assert.Equal("Complete", data.GetProperty("stage").GetString());
        Assert.Equal("TripNest", data.GetProperty("bookedThrough").GetString());
        Assert.Equal(422m, data.GetProperty("nightlyRate").GetDecimal());
        Assert.Equal(1266m, data.GetProperty("netRevenue").GetDecimal());
        Assert.Equal(253.20m, data.GetProperty("managementFee").GetDecimal());
        Assert.Equal(1012.80m, data.GetProperty("ownerPayout").GetDecimal());

        var reviews = data.GetProperty("guestReviews");
        Assert.Equal(1, reviews.GetArrayLength());
        Assert.Equal(5, reviews[0].GetProperty("rating").GetInt32());
        Assert.Equal("Wonderful stay", reviews[0].GetProperty("comment").GetString());
    }

    [Fact]
    public async Task ReservationDetails_OtherLandlordsBooking_IsForbidden()
    {
        var (tenantId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        var (ownerId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        var (bookingId, _) = await SeedBookingAsync(tenantId, ownerId, BookingStatus.Confirmed, daysFromNow: 5, guests: 2);

        // A different landlord must not see it.
        await RegisterAndLoginAsync(UserRole.Landlord);
        var response = await _httpClient.GetAsync($"/api/landlord/reservations/{bookingId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_InvalidGuestCount_IsRejected()
    {
        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        string propertyId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var property = NewProperty(landlordId);
            db.Set<Property>().Add(property);
            propertyId = property.Id;
            await db.SaveChangesAsync();
        }
        await RegisterAndLoginAsync(UserRole.Tenant);

        var response = await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = DateTime.UtcNow.AddDays(3),
            checkOutDate = DateTime.UtcNow.AddDays(6),
            guests = 0
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Creates a property for the landlord plus a booking starting daysFromNow.
    /// Returns (bookingId, propertyId).</summary>
    private async Task<(string BookingId, string PropertyId)> SeedBookingAsync(
        string tenantId, string landlordId, BookingStatus status, int daysFromNow,
        int guests, decimal total = 500m, int nights = 3)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = NewProperty(landlordId);
        db.Set<Property>().Add(property);

        var booking = new Booking
        {
            TenantId = tenantId,
            PropertyId = property.Id,
            Status = status,
            Guests = guests,
            TotalAmount = total,
            CheckInDate = DateTime.UtcNow.AddDays(daysFromNow),
            CheckOutDate = DateTime.UtcNow.AddDays(daysFromNow + nights)
        };
        db.Set<Booking>().Add(booking);

        await db.SaveChangesAsync();
        return (booking.Id, property.Id);
    }

    private static Property NewProperty(string ownerId) => new()
    {
        UserId = ownerId,
        Title = "Reservations Test Property",
        Description = "d",
        Location = "Accra",
        Latitude = 5.6,
        Longitude = -0.2,
        Bedrooms = 2,
        Bathrooms = 1,
        MonthlyRent = 900m,
        DailyRate = 100m,
        PropertyType = "Apartment"
    };
}
