using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for the discovery/differentiator features: advanced search filters (stay type,
/// amenities, map bounds, dates), true-total stay quotes, the platform cancellation grace
/// period, loyalty tiers, and the iCal export feed.
/// </summary>
public class DiscoveryAndLoyaltyTests : TestBase
{
    // ------------------------------------------------------------- helpers

    private async Task<(string PropertyId, string OwnerId)> CreatePropertyAsync(
        string location = "Accra, Ghana",
        StayType stayType = StayType.ShortTerm,
        string? amenities = "WiFi,Kitchen",
        double lat = 5.6037,
        double lng = -0.1870,
        decimal dailyRate = 120m,
        CancellationPolicy policy = CancellationPolicy.Moderate)
    {
        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(landlordId);

        var res = await _httpClient.PostAsJsonAsync("/api/properties", new CreatePropertyRequest
        {
            Title = "Test Listing",
            Description = "Bright and close to everything",
            Location = location,
            Latitude = lat,
            Longitude = lng,
            Bedrooms = 2,
            Bathrooms = 1,
            MonthlyRent = 3000m,
            DailyRate = dailyRate,
            PropertyType = "Apartment",
            StayType = stayType,
            CancellationPolicy = policy,
            Amenities = amenities
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var data = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        var propertyId = data.GetProperty("propertyId").GetString()!;

        // Search only surfaces Active listings; skip the walkthrough-approval gate for tests.
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var property = await db.Properties.FindAsync(propertyId);
            property!.Status = PropertyStatus.Active;
            await db.SaveChangesAsync();
        }

        return (propertyId, landlordId);
    }

    private static async Task<JsonElement> DataOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data");
    }

    // ------------------------------------------------------- search filters

    [Fact]
    public async Task Search_FiltersByStayTypeAndAmenities()
    {
        var (shortTermId, _) = await CreatePropertyAsync(stayType: StayType.ShortTerm, amenities: "WiFi,Pool");
        var (studentId, _) = await CreatePropertyAsync(stayType: StayType.Student, amenities: "WiFi,StudyArea");

        var byType = await DataOf(await _httpClient.GetAsync("/api/properties/search?stayType=Student"));
        var typeIds = byType.EnumerateArray().Select(p => p.GetProperty("propertyId").GetString()).ToList();
        Assert.Contains(studentId, typeIds);
        Assert.DoesNotContain(shortTermId, typeIds);

        var byAmenity = await DataOf(await _httpClient.GetAsync("/api/properties/search?amenities=WiFi,Pool"));
        var amenityIds = byAmenity.EnumerateArray().Select(p => p.GetProperty("propertyId").GetString()).ToList();
        Assert.Contains(shortTermId, amenityIds);
        Assert.DoesNotContain(studentId, amenityIds);
    }

    [Fact]
    public async Task Search_FiltersByMapBounds()
    {
        var (accraId, _) = await CreatePropertyAsync(lat: 5.6037, lng: -0.1870);
        var (kumasiId, _) = await CreatePropertyAsync(location: "Kumasi, Ghana", lat: 6.6885, lng: -1.6244);

        // Viewport around Accra only.
        var inView = await DataOf(await _httpClient.GetAsync(
            "/api/properties/search?minLat=5.4&maxLat=5.8&minLng=-0.4&maxLng=0.1"));
        var ids = inView.EnumerateArray().Select(p => p.GetProperty("propertyId").GetString()).ToList();
        Assert.Contains(accraId, ids);
        Assert.DoesNotContain(kumasiId, ids);
    }

    [Fact]
    public async Task Search_WithDates_ExcludesBlockedListings_AndQuotesTheRest()
    {
        var (openId, _) = await CreatePropertyAsync();
        var (blockedId, ownerId) = await CreatePropertyAsync();

        var checkIn = DateTime.UtcNow.Date.AddDays(30);
        var checkOut = checkIn.AddDays(2);

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<PropertyBlockedDate>().Add(new PropertyBlockedDate
            {
                PropertyId = blockedId,
                BlockedByUserId = ownerId,
                StartDate = checkIn,
                EndDate = checkOut
            });
            await db.SaveChangesAsync();
        }

        var results = await DataOf(await _httpClient.GetAsync(
            $"/api/properties/search?checkIn={checkIn:yyyy-MM-dd}&checkOut={checkOut:yyyy-MM-dd}"));
        var ids = results.EnumerateArray().Select(p => p.GetProperty("propertyId").GetString()).ToList();
        Assert.Contains(openId, ids);
        Assert.DoesNotContain(blockedId, ids);

        // Every dated result carries the all-in quote for the stay (2 nights x 120, no fees).
        var open = results.EnumerateArray().First(p => p.GetProperty("propertyId").GetString() == openId);
        var quote = open.GetProperty("quote");
        Assert.Equal(2, quote.GetProperty("nights").GetInt32());
        Assert.Equal(240m, quote.GetProperty("total").GetDecimal());
    }

    // ------------------------------------------------------- true total pricing

    [Fact]
    public async Task Quote_IncludesCleaningFeeWeekendRateAndDiscounts()
    {
        var (propertyId, _) = await CreatePropertyAsync();

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<PricingSettings>().Add(new PricingSettings
            {
                PropertyId = propertyId,
                BaseRate = 100m,
                WeekendRate = 150m,
                CleaningFee = 40m,
                WeeklyDiscountPercent = 10m
            });
            await db.SaveChangesAsync();
        }

        // A full week starting Monday: 5 weekday nights x 100 + Fri/Sat x 150 = 800,
        // minus 10% weekly discount (80), plus the 40 cleaning fee => 760.
        var monday = DateTime.UtcNow.Date.AddDays(14);
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(1);

        var quote = await DataOf(await _httpClient.GetAsync(
            $"/api/properties/{propertyId}/quote?checkIn={monday:yyyy-MM-dd}&checkOut={monday.AddDays(7):yyyy-MM-dd}"));

        Assert.Equal(7, quote.GetProperty("nights").GetInt32());
        Assert.Equal(800m, quote.GetProperty("staySubtotal").GetDecimal());
        Assert.Equal(80m, quote.GetProperty("lengthOfStayDiscount").GetDecimal());
        Assert.Equal(40m, quote.GetProperty("cleaningFee").GetDecimal());
        Assert.Equal(760m, quote.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Booking_ChargesExactlyTheQuotedTotal()
    {
        var (propertyId, _) = await CreatePropertyAsync();

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<PricingSettings>().Add(new PricingSettings
            {
                PropertyId = propertyId,
                BaseRate = 100m,
                CleaningFee = 25m
            });
            await db.SaveChangesAsync();
        }

        await RegisterAndLoginAsync(UserRole.Tenant);
        var monday = DateTime.UtcNow.Date.AddDays(21);
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(1);

        var quoted = await DataOf(await _httpClient.GetAsync(
            $"/api/properties/{propertyId}/quote?checkIn={monday:yyyy-MM-dd}&checkOut={monday.AddDays(2):yyyy-MM-dd}"));

        var booked = await DataOf(await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = monday.ToString("yyyy-MM-dd"),
            checkOutDate = monday.AddDays(2).ToString("yyyy-MM-dd"),
            guests = 2
        }));

        Assert.Equal(quoted.GetProperty("total").GetDecimal(), booked.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(225m, booked.GetProperty("totalAmount").GetDecimal()); // 2 x 100 + 25
    }

    // --------------------------------------------------- cancellation grace

    [Fact]
    public async Task CancellationPreview_WithinGraceWindow_RefundsInFull_EvenOnStrictPolicy()
    {
        var (propertyId, _) = await CreatePropertyAsync(policy: CancellationPolicy.Strict);
        await RegisterAndLoginAsync(UserRole.Tenant);

        var checkIn = DateTime.UtcNow.Date.AddDays(6); // Strict alone would refund only 50% here
        var booked = await DataOf(await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = checkIn.ToString("yyyy-MM-dd"),
            checkOutDate = checkIn.AddDays(2).ToString("yyyy-MM-dd"),
            guests = 1
        }));
        var bookingId = booked.GetProperty("bookingId").GetString()!;

        // Just booked => inside the 48h grace window => 100% regardless of Strict.
        var preview = await DataOf(await _httpClient.GetAsync($"/api/bookings/{bookingId}/cancellation-preview"));
        Assert.Equal(100m, preview.GetProperty("refundPercentage").GetDecimal());
        Assert.Equal("GracePeriod", preview.GetProperty("policyName").GetString());

        // Age the booking past the grace window => the listing's Strict policy applies again.
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var booking = await db.Bookings.FindAsync(bookingId);
            booking!.CreatedAt = DateTime.UtcNow.AddHours(-72);
            await db.SaveChangesAsync();
        }

        var later = await DataOf(await _httpClient.GetAsync($"/api/bookings/{bookingId}/cancellation-preview"));
        Assert.Equal(0m, later.GetProperty("refundPercentage").GetDecimal());
        Assert.Equal("Strict", later.GetProperty("policyName").GetString());
    }

    // --------------------------------------------------------------- loyalty

    [Fact]
    public async Task Loyalty_TiersProgressWithCompletedStays_AndDiscountTheQuote()
    {
        var (propertyId, _) = await CreatePropertyAsync(dailyRate: 100m);
        var (tenantId, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        var fresh = await DataOf(await _httpClient.GetAsync("/api/loyalty/me"));
        Assert.Equal("Bronze", fresh.GetProperty("tier").GetString());
        Assert.Equal(0m, fresh.GetProperty("discountPercent").GetDecimal());
        Assert.Equal("Silver", fresh.GetProperty("nextTier").GetString());

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 3; i++)
                db.Bookings.Add(new Booking
                {
                    TenantId = tenantId,
                    PropertyId = propertyId,
                    CheckInDate = DateTime.UtcNow.AddDays(-30 - i * 10),
                    CheckOutDate = DateTime.UtcNow.AddDays(-28 - i * 10),
                    TotalAmount = 200m,
                    Status = BookingStatus.Completed
                });
            await db.SaveChangesAsync();
        }

        var silver = await DataOf(await _httpClient.GetAsync("/api/loyalty/me"));
        Assert.Equal("Silver", silver.GetProperty("tier").GetString());
        Assert.Equal(3m, silver.GetProperty("discountPercent").GetDecimal());

        // Authenticated quote now carries the loyalty discount: 2 nights x 100 => 200, minus 3%.
        var monday = DateTime.UtcNow.Date.AddDays(14);
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(1);
        var quote = await DataOf(await _httpClient.GetAsync(
            $"/api/properties/{propertyId}/quote?checkIn={monday:yyyy-MM-dd}&checkOut={monday.AddDays(2):yyyy-MM-dd}"));
        Assert.Equal(6m, quote.GetProperty("loyaltyDiscount").GetDecimal());
        Assert.Equal(194m, quote.GetProperty("total").GetDecimal());
    }

    // ------------------------------------------------------------------ iCal

    [Fact]
    public async Task IcalFeed_ExportsBlockedRanges_AndRejectsBadTokens()
    {
        var (propertyId, ownerId) = await CreatePropertyAsync();

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<PropertyBlockedDate>().Add(new PropertyBlockedDate
            {
                PropertyId = propertyId,
                BlockedByUserId = ownerId,
                StartDate = DateTime.UtcNow.Date.AddDays(10),
                EndDate = DateTime.UtcNow.Date.AddDays(12)
            });
            await db.SaveChangesAsync();
        }

        // Owner (still authenticated from CreatePropertyAsync) fetches the tokenized URL…
        var feed = await DataOf(await _httpClient.GetAsync($"/api/calendar/{propertyId}/feed-url"));
        var feedUrl = feed.GetProperty("feedUrl").GetString()!;

        // …which serves a valid VCALENDAR anonymously.
        ClearAuth();
        var ics = await _httpClient.GetAsync(new Uri(feedUrl).PathAndQuery);
        Assert.Equal(HttpStatusCode.OK, ics.StatusCode);
        Assert.StartsWith("text/calendar", ics.Content.Headers.ContentType!.MediaType);
        var body = await ics.Content.ReadAsStringAsync();
        Assert.Contains("BEGIN:VCALENDAR", body);
        Assert.Contains("Not available", body);

        // A wrong token is rejected.
        var forged = await _httpClient.GetAsync($"/api/calendar/{propertyId}.ics?token=deadbeef");
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);
    }
}
