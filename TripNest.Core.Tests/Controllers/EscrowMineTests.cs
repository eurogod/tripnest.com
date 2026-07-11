using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for GET /api/escrow/mine — the caller's own escrows (as the paying tenant) for the
/// payments "held funds" view. Must be scoped to the caller and ordered newest-first.
/// </summary>
public class EscrowMineTests : TestBase
{
    [Fact]
    public async Task GetMine_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _httpClient.GetAsync("/api/escrow/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMine_NoBookings_ReturnsEmptyList()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);

        var response = await _httpClient.GetAsync("/api/escrow/mine");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("items");
        Assert.Equal(0, data.GetArrayLength());
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyCallersEscrows_NewestFirst()
    {
        var (otherId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        var (callerId, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        string olderEscrowId, newerEscrowId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var property = NewProperty("landlord-mine");
            db.Set<Property>().Add(property);

            olderEscrowId = AddBookingWithEscrow(db, callerId, property.Id, 100m, DateTime.UtcNow.AddDays(-2));
            newerEscrowId = AddBookingWithEscrow(db, callerId, property.Id, 200m, DateTime.UtcNow.AddDays(-1));
            AddBookingWithEscrow(db, otherId, property.Id, 300m, DateTime.UtcNow);

            await db.SaveChangesAsync();
        }

        // RegisterAndLoginAsync leaves the client authenticated as the caller (last login).
        var response = await _httpClient.GetAsync("/api/escrow/mine");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected OK but got {response.StatusCode}: {body}");
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("items");

        Assert.Equal(2, data.GetArrayLength());
        Assert.Equal(newerEscrowId, data[0].GetProperty("escrowId").GetString());
        Assert.Equal(olderEscrowId, data[1].GetProperty("escrowId").GetString());
    }

    private static string AddBookingWithEscrow(
        AppDbContext db, string tenantId, string propertyId, decimal amount, DateTime createdAt)
    {
        var booking = new Booking
        {
            TenantId = tenantId,
            PropertyId = propertyId,
            TotalAmount = amount,
            Status = BookingStatus.Confirmed
        };
        db.Set<Booking>().Add(booking);

        var escrow = new Escrow
        {
            BookingId = booking.Id,
            Amount = amount,
            Status = EscrowStatus.HeldInEscrow,
            CreatedAt = createdAt
        };
        db.Set<Escrow>().Add(escrow);
        return escrow.Id;
    }

    private static Property NewProperty(string ownerId) => new()
    {
        UserId = ownerId,
        Title = "Escrow Mine Test Property",
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
}
