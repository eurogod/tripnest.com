using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for monthly rent collection: long-term bookings charge only the first period
/// upfront, generate a pro-rated invoice schedule, collect month by month with landlord
/// payouts net of the platform fee, and void outstanding invoices on cancellation.
/// </summary>
public class RentCollectionTests : TestBase
{
    private StubPaymentGateway Gateway => _fixture.Services.GetRequiredService<StubPaymentGateway>();

    [Fact]
    public async Task LongTermBooking_ChargesFirstMonthOnly_AndSchedulesTheRest()
    {
        var propertyId = await CreateLongTermPropertyAsync(monthlyRent: 900m);
        await RegisterAndLoginAsync(UserRole.Tenant);

        // 75 nights = period 1 (escrow, 900) + one full period (900) + 15-day partial (450).
        var checkIn = DateTime.UtcNow.Date.AddDays(10);
        var booked = await DataOf(await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = checkIn.ToString("yyyy-MM-dd"),
            checkOutDate = checkIn.AddDays(75).ToString("yyyy-MM-dd"),
            guests = 1
        }));
        var bookingId = booked.GetProperty("bookingId").GetString()!;

        // Upfront charge is one month's rent, not the whole stay.
        Assert.Equal(900m, booked.GetProperty("totalAmount").GetDecimal());

        var schedule = await DataOf(await _httpClient.GetAsync($"/api/rent/booking/{bookingId}"));
        var invoices = schedule.EnumerateArray().ToList();
        Assert.Equal(2, invoices.Count);
        Assert.Equal(900m, invoices[0].GetProperty("amount").GetDecimal());
        Assert.Equal(450m, invoices[1].GetProperty("amount").GetDecimal());
        Assert.All(invoices, i => Assert.Equal((int)RentInvoiceStatus.Upcoming, i.GetProperty("status").GetInt32()));
    }

    [Fact]
    public async Task ShortTermBooking_HasNoRentSchedule()
    {
        var propertyId = await CreateLongTermPropertyAsync(stayType: StayType.ShortTerm, dailyRate: 100m);
        await RegisterAndLoginAsync(UserRole.Tenant);

        var checkIn = DateTime.UtcNow.Date.AddDays(10);
        var booked = await DataOf(await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = checkIn.ToString("yyyy-MM-dd"),
            checkOutDate = checkIn.AddDays(3).ToString("yyyy-MM-dd"),
            guests = 1
        }));
        var bookingId = booked.GetProperty("bookingId").GetString()!;

        Assert.Equal(HttpStatusCode.NotFound, (await _httpClient.GetAsync($"/api/rent/booking/{bookingId}")).StatusCode);
    }

    [Fact]
    public async Task PayingAnInvoice_MarksItPaid_AndCreatesLandlordPayoutNetOfFee()
    {
        var (propertyId, landlordId) = await CreateLongTermPropertyWithOwnerAsync(monthlyRent: 1000m);
        await RegisterAndLoginAsync(UserRole.Tenant);

        var checkIn = DateTime.UtcNow.Date.AddDays(5);
        var booked = await DataOf(await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = checkIn.ToString("yyyy-MM-dd"),
            checkOutDate = checkIn.AddDays(90).ToString("yyyy-MM-dd"),
            guests = 1
        }));
        var bookingId = booked.GetProperty("bookingId").GetString()!;

        var schedule = await DataOf(await _httpClient.GetAsync($"/api/rent/booking/{bookingId}"));
        var invoiceId = schedule.EnumerateArray().First().GetProperty("invoiceId").GetString()!;

        var initiated = await DataOf(await _httpClient.PostAsync($"/api/rent/invoices/{invoiceId}/pay", null));
        Assert.False(string.IsNullOrEmpty(initiated.GetProperty("checkoutUrl").GetString()));

        Gateway.VerifyAmount = 1000m;
        var verified = await DataOf(await _httpClient.PostAsync($"/api/rent/invoices/{invoiceId}/verify", null));
        Assert.Equal((int)RentInvoiceStatus.Paid, verified.GetProperty("status").GetInt32());

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payout = db.Payouts.Single(p => p.RentInvoiceId == invoiceId);
        Assert.Equal(landlordId, payout.LandlordId);
        Assert.Equal(1000m, payout.GrossAmount);
        Assert.Equal(100m, payout.FeeAmount);   // 10% platform fee
        Assert.Equal(900m, payout.Amount);      // net to the landlord
        Assert.Null(payout.EscrowId);
    }

    [Fact]
    public async Task Invoice_OnlyItsTenantCanPayIt()
    {
        var propertyId = await CreateLongTermPropertyAsync();
        await RegisterAndLoginAsync(UserRole.Tenant);

        var checkIn = DateTime.UtcNow.Date.AddDays(5);
        var booked = await DataOf(await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = checkIn.ToString("yyyy-MM-dd"),
            checkOutDate = checkIn.AddDays(90).ToString("yyyy-MM-dd"),
            guests = 1
        }));
        var bookingId = booked.GetProperty("bookingId").GetString()!;
        var schedule = await DataOf(await _httpClient.GetAsync($"/api/rent/booking/{bookingId}"));
        var invoiceId = schedule.EnumerateArray().First().GetProperty("invoiceId").GetString()!;

        // A different tenant cannot pay (or even see) someone else's rent.
        await RegisterAndLoginAsync(UserRole.Tenant);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _httpClient.PostAsync($"/api/rent/invoices/{invoiceId}/pay", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _httpClient.GetAsync($"/api/rent/booking/{bookingId}")).StatusCode);
    }

    [Fact]
    public async Task CancellingTheBooking_VoidsOutstandingInvoices()
    {
        var propertyId = await CreateLongTermPropertyAsync();
        await RegisterAndLoginAsync(UserRole.Tenant);

        var checkIn = DateTime.UtcNow.Date.AddDays(15);
        var booked = await DataOf(await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = checkIn.ToString("yyyy-MM-dd"),
            checkOutDate = checkIn.AddDays(90).ToString("yyyy-MM-dd"),
            guests = 1
        }));
        var bookingId = booked.GetProperty("bookingId").GetString()!;

        await DataOf(await _httpClient.PostAsync($"/api/bookings/{bookingId}/cancel", null));

        var schedule = await DataOf(await _httpClient.GetAsync($"/api/rent/booking/{bookingId}"));
        Assert.All(schedule.EnumerateArray(),
            i => Assert.Equal((int)RentInvoiceStatus.Cancelled, i.GetProperty("status").GetInt32()));
    }

    // ------------------------------------------------------------------ helpers

    private async Task<string> CreateLongTermPropertyAsync(
        decimal monthlyRent = 1200m, StayType stayType = StayType.LongTerm, decimal? dailyRate = null)
    {
        var (propertyId, _) = await CreateLongTermPropertyWithOwnerAsync(monthlyRent, stayType, dailyRate);
        return propertyId;
    }

    private async Task<(string PropertyId, string LandlordId)> CreateLongTermPropertyWithOwnerAsync(
        decimal monthlyRent = 1200m, StayType stayType = StayType.LongTerm, decimal? dailyRate = null)
    {
        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(landlordId);

        var res = await _httpClient.PostAsJsonAsync("/api/properties", new CreatePropertyRequest
        {
            Title = "Long Stay Apartment",
            Description = "Furnished for long-term tenants",
            Location = "Accra, Ghana",
            Latitude = 5.6,
            Longitude = -0.19,
            Bedrooms = 2,
            Bathrooms = 1,
            MonthlyRent = monthlyRent,
            DailyRate = dailyRate,
            PropertyType = "Apartment",
            StayType = stayType,
            CancellationPolicy = CancellationPolicy.Moderate
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var propertyId = JsonDocument.Parse(await res.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("propertyId").GetString()!;

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await db.Properties.FindAsync(propertyId);
        property!.Status = PropertyStatus.Active;
        await db.SaveChangesAsync();
        return (propertyId, landlordId);
    }

    private static async Task<JsonElement> DataOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data");
    }
}
