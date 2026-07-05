using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

public class PersonalDashboardTests : TestBase
{
    [Fact]
    public async Task AgentDashboard_AggregatesWalkthroughsFromDatabase()
    {
        var (agentId, _) = await RegisterAndLoginAsync(UserRole.Agent);

        // Empty case first: the SQL aggregate must handle no rows (MAX over empty -> null, SUM -> 0)
        // and return zeros rather than 500.
        var empty = await _httpClient.GetAsync("/api/personaldashboard/agent");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        var emptyData = JsonDocument.Parse(await empty.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data");
        Assert.Equal(0, emptyData.GetProperty("totalWalkthroughs").GetInt32());
        Assert.Equal(0d, emptyData.GetProperty("recentActivity").GetProperty("totalVideoHours").GetDouble());

        // Seed one property with two walkthroughs directly in the store.
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var property = new Property
            {
                UserId = agentId,
                Title = "Test Property",
                Description = "For dashboard aggregation",
                Location = "Accra",
                Latitude = 5.6,
                Longitude = -0.2,
                Bedrooms = 2,
                Bathrooms = 1,
                MonthlyRent = 1000m,
                DailyRate = 50m,
                PropertyType = "Apartment"
            };
            db.Set<Property>().Add(property);
            db.Set<Walkthrough>().AddRange(
                new Walkthrough { PropertyId = property.Id, Title = "W1", VideoPath = "/uploads/x.mp4", DurationSeconds = 3600 },
                new Walkthrough { PropertyId = property.Id, Title = "W2", VideoPath = "/uploads/y.mp4", DurationSeconds = 1800 });
            await db.SaveChangesAsync();
        }

        var res = await _httpClient.GetAsync("/api/personaldashboard/agent");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var data = JsonDocument.Parse(await res.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data");

        Assert.Equal(2, data.GetProperty("totalWalkthroughs").GetInt32());
        Assert.Equal(1, data.GetProperty("propertiesWithWalkthroughs").GetInt32());
        // 3600 + 1800 = 5400s = 1.5h — previously always 0 because the navigation was never loaded.
        Assert.Equal(1.5d, data.GetProperty("recentActivity").GetProperty("totalVideoHours").GetDouble());
    }

    [Fact]
    public async Task LandlordDashboard_ReportsHeldEscrowAndWalkthroughs()
    {
        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);

        string propertyId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var property = new Property
            {
                UserId = landlordId,
                Title = "Held Escrow Property",
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
            propertyId = property.Id;

            var booking = new Booking
            {
                TenantId = "some-tenant",
                PropertyId = property.Id,
                TotalAmount = 500m,
                Status = BookingStatus.Confirmed
            };
            db.Set<Booking>().Add(booking);
            db.Set<Escrow>().Add(new Escrow { BookingId = booking.Id, Amount = 500m, Status = EscrowStatus.HeldInEscrow });
            db.Set<Walkthrough>().Add(new Walkthrough { PropertyId = property.Id, Title = "W", VideoPath = "/uploads/v.mp4", DurationSeconds = 60 });
            await db.SaveChangesAsync();
        }

        var res = await _httpClient.GetAsync("/api/personaldashboard/landlord");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var data = JsonDocument.Parse(await res.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data");

        // Both were previously always 0: EscrowHeld ("Held" != enum "HeldInEscrow") and
        // PendingWalkthroughs (Walkthroughs navigation never loaded).
        Assert.Equal(500m, data.GetProperty("escrowHeld").GetDecimal());
        Assert.Equal(1, data.GetProperty("pendingWalkthroughs").GetInt32());
    }
}
