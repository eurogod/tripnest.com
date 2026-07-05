using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Drives the whole core guest flow end to end over HTTP: create a booking, initiate the
/// escrow payment (must hand back a payable checkout URL), verify the charge (escrow held,
/// booking Confirmed), create the rental agreement, and confirm that the now-Confirmed
/// booking blocks another tenant from double-booking the same dates.
/// </summary>
public class BookingLifecycleE2ETests : TestBase
{
    [Fact]
    public async Task FullGuestFlow_BookPayConfirmAgreement_ThenDatesAreBlocked()
    {
        // A property to book, owned by some landlord.
        string propertyId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var property = NewProperty("landlord-e2e");
            db.Set<Property>().Add(property);
            propertyId = property.Id;
            await db.SaveChangesAsync();
        }

        var checkIn = DateTime.UtcNow.Date.AddDays(7);
        var checkOut = checkIn.AddDays(3);

        // 1. Tenant books — booking starts out Pending.
        await RegisterAndLoginAsync(UserRole.Tenant);
        var bookRes = await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = checkIn,
            checkOutDate = checkOut,
            guests = 2
        });
        var bookBody = await bookRes.Content.ReadAsStringAsync();
        Assert.True(bookRes.StatusCode == HttpStatusCode.Created, $"Expected Created but got {bookRes.StatusCode}: {bookBody}");
        var booking = JsonDocument.Parse(bookBody).RootElement.GetProperty("data");
        var bookingId = booking.GetProperty("bookingId").GetString()!;
        var totalAmount = booking.GetProperty("totalAmount").GetDecimal();
        Assert.Equal((int)BookingStatus.Pending, booking.GetProperty("status").GetInt32());

        // 2. Initiate payment — the tenant must get a checkout URL and a payment reference,
        //    even though the escrow row was already created alongside the booking.
        var initRes = await _httpClient.PostAsJsonAsync("/api/escrow/initiate", new { bookingId });
        var initBody = await initRes.Content.ReadAsStringAsync();
        Assert.True(initRes.StatusCode == HttpStatusCode.OK, $"Expected OK but got {initRes.StatusCode}: {initBody}");
        var escrow = JsonDocument.Parse(initBody).RootElement.GetProperty("data");
        Assert.False(string.IsNullOrEmpty(escrow.GetProperty("checkoutUrl").GetString()), "initiate must return a checkout URL");
        Assert.False(string.IsNullOrEmpty(escrow.GetProperty("paymentReference").GetString()), "initiate must stamp a payment reference");

        // 3. Provider confirms the charge for the amount owed — escrow held, booking Confirmed.
        var stub = _fixture.Services.GetRequiredService<StubPaymentGateway>();
        stub.VerifySucceeds = true;
        stub.VerifyAmount = totalAmount;
        var verifyRes = await _httpClient.PostAsync($"/api/escrow/booking/{bookingId}/verify", null);
        var verifyBody = await verifyRes.Content.ReadAsStringAsync();
        Assert.True(verifyRes.StatusCode == HttpStatusCode.OK, $"Expected OK but got {verifyRes.StatusCode}: {verifyBody}");
        var held = JsonDocument.Parse(verifyBody).RootElement.GetProperty("data");
        Assert.Equal((int)EscrowStatus.HeldInEscrow, held.GetProperty("status").GetInt32());

        var afterPay = await GetBookingAsync(bookingId);
        Assert.Equal((int)BookingStatus.Confirmed, afterPay.GetProperty("status").GetInt32());

        // 4. With the booking Confirmed, the rental agreement can be created.
        var agreeRes = await _httpClient.PostAsJsonAsync("/api/agreements", new { bookingId });
        var agreeBody = await agreeRes.Content.ReadAsStringAsync();
        Assert.True(agreeRes.StatusCode == HttpStatusCode.Created, $"Expected Created but got {agreeRes.StatusCode}: {agreeBody}");

        // 5. Another tenant trying the same property + overlapping dates is now rejected.
        await RegisterAndLoginAsync(UserRole.Tenant);
        var doubleRes = await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = checkIn.AddDays(1),
            checkOutDate = checkOut.AddDays(1),
            guests = 1
        });
        Assert.Equal(HttpStatusCode.Conflict, doubleRes.StatusCode);
    }

    private async Task<JsonElement> GetBookingAsync(string bookingId)
    {
        var res = await _httpClient.GetAsync($"/api/bookings/{bookingId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
    }

    private static Property NewProperty(string ownerId) => new()
    {
        UserId = ownerId,
        Title = "Lifecycle Test Property",
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
}
