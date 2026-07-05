using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

public class EscrowVerifyTests : TestBase
{
    [Fact]
    public async Task VerifyPayment_ConfirmsWithProviderAndHoldsEscrow_Idempotently()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        string bookingId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var property = NewProperty("landlord-1");
            db.Set<Property>().Add(property);
            var booking = new Booking
            {
                TenantId = userId,
                PropertyId = property.Id,
                TotalAmount = 750m,
                Status = BookingStatus.Confirmed
            };
            db.Set<Booking>().Add(booking);
            bookingId = booking.Id;
            db.Set<Escrow>().Add(new Escrow
            {
                BookingId = booking.Id,
                Amount = 750m,
                Status = EscrowStatus.Pending,
                PaymentReference = "REF-123"
            });
            await db.SaveChangesAsync();
        }

        // Provider confirms the charge for the escrow's amount.
        var stub = _fixture.Services.GetRequiredService<StubPaymentGateway>();
        stub.VerifySucceeds = true;
        stub.VerifyAmount = 750m;

        var res = await _httpClient.PostAsync($"/api/escrow/booking/{bookingId}/verify", null);
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.OK, $"Expected OK but got {res.StatusCode}: {body}");
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");
        Assert.Equal((int)EscrowStatus.HeldInEscrow, data.GetProperty("status").GetInt32());

        // Idempotent: a second verify (e.g. webhook already held it) still returns OK, still held.
        var again = await _httpClient.PostAsync($"/api/escrow/booking/{bookingId}/verify", null);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var againData = JsonDocument.Parse(await again.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.Equal((int)EscrowStatus.HeldInEscrow, againData.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task VerifyPayment_WhenProviderSaysNotPaid_ReturnsBadRequest()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        string bookingId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var property = NewProperty("landlord-2");
            db.Set<Property>().Add(property);
            var booking = new Booking { TenantId = userId, PropertyId = property.Id, TotalAmount = 200m, Status = BookingStatus.Confirmed };
            db.Set<Booking>().Add(booking);
            bookingId = booking.Id;
            db.Set<Escrow>().Add(new Escrow { BookingId = booking.Id, Amount = 200m, Status = EscrowStatus.Pending, PaymentReference = "REF-UNPAID" });
            await db.SaveChangesAsync();
        }

        var stub = _fixture.Services.GetRequiredService<StubPaymentGateway>();
        stub.VerifySucceeds = false;

        var res = await _httpClient.PostAsync($"/api/escrow/booking/{bookingId}/verify", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    private static Property NewProperty(string ownerId) => new()
    {
        UserId = ownerId,
        Title = "Escrow Test Property",
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
