using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Safe-arrival check-in: tapping check-in notifies the chosen contact (saved on the profile by
/// default, overridable per request), and location is attached only when the traveller consents.
/// Uses the recording SMS/email doubles registered in <see cref="TestFixture"/>.
/// </summary>
public class SafetyCheckInTests : TestBase
{
    private RecordingSmsSender Sms => _fixture.Services.GetRequiredService<RecordingSmsSender>();
    private RecordingEmailSender Email => _fixture.Services.GetRequiredService<RecordingEmailSender>();

    private async Task<string> SeedBookingAsync(string tenantId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = new Booking
        {
            TenantId = tenantId,
            PropertyId = "prop-" + Guid.NewGuid().ToString("N"),
            CheckInDate = DateTime.UtcNow,
            CheckOutDate = DateTime.UtcNow.AddDays(1),
            TotalAmount = 100m,
            Status = BookingStatus.Confirmed
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking.Id;
    }

    private async Task SaveTrustedContactAsync(string? phone, string? email)
    {
        var res = await _httpClient.PutAsJsonAsync("/api/safety/contact",
            new { name = "Trusted Person", phone, email });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    private static JsonElement Data(string json) =>
        JsonDocument.Parse(json).RootElement.GetProperty("data");

    [Fact]
    public async Task CheckIn_WithSavedContact_NotifiesContact()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await SaveTrustedContactAsync("0244123456", "contact@example.com");
        var bookingId = await SeedBookingAsync(userId);

        Sms.Sent.Clear();
        Email.Sent.Clear();

        var res = await _httpClient.PostAsJsonAsync("/api/safety/checkin",
            new { bookingId, shareLocation = false });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var sms = Assert.Single(Sms.Sent);
        Assert.Equal("+233244123456", sms.Phone);
        Assert.Contains("checked in safely", sms.Message);
        Assert.DoesNotContain("maps.google.com", sms.Message);

        var email = Assert.Single(Email.Sent);
        Assert.Equal("contact@example.com", email.To);

        var data = Data(await res.Content.ReadAsStringAsync());
        Assert.True(data.GetProperty("contactNotified").GetBoolean());
        Assert.False(data.GetProperty("locationShared").GetBoolean());
    }

    [Fact]
    public async Task CheckIn_WithConsentedLocation_IncludesMapsLink()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await SaveTrustedContactAsync("0244123456", null);
        var bookingId = await SeedBookingAsync(userId);

        Sms.Sent.Clear();

        var res = await _httpClient.PostAsJsonAsync("/api/safety/checkin",
            new { bookingId, shareLocation = true, latitude = 5.6037, longitude = -0.1870 });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var sms = Assert.Single(Sms.Sent);
        Assert.Contains("maps.google.com/?q=5.6037,-0.187", sms.Message);

        var data = Data(await res.Content.ReadAsStringAsync());
        Assert.True(data.GetProperty("locationShared").GetBoolean());
    }

    [Fact]
    public async Task CheckIn_WithoutConsent_OmitsLocation_EvenIfCoordsSent()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await SaveTrustedContactAsync("0244123456", null);
        var bookingId = await SeedBookingAsync(userId);

        Sms.Sent.Clear();

        // Coords supplied but ShareLocation=false → server must ignore them.
        var res = await _httpClient.PostAsJsonAsync("/api/safety/checkin",
            new { bookingId, shareLocation = false, latitude = 5.6037, longitude = -0.1870 });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var sms = Assert.Single(Sms.Sent);
        Assert.DoesNotContain("maps.google.com", sms.Message);

        var data = Data(await res.Content.ReadAsStringAsync());
        Assert.False(data.GetProperty("locationShared").GetBoolean());
    }

    [Fact]
    public async Task CheckIn_RequestContact_OverridesSavedContact()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await SaveTrustedContactAsync("0244123456", null);
        var bookingId = await SeedBookingAsync(userId);

        Sms.Sent.Clear();

        var res = await _httpClient.PostAsJsonAsync("/api/safety/checkin",
            new { bookingId, contactPhone = "0209876543", shareLocation = false });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var sms = Assert.Single(Sms.Sent);
        Assert.Equal("+233209876543", sms.Phone);
    }

    [Fact]
    public async Task CheckIn_NoContact_RecordsOnly_NoSend()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        var bookingId = await SeedBookingAsync(userId);

        Sms.Sent.Clear();
        Email.Sent.Clear();

        var res = await _httpClient.PostAsJsonAsync("/api/safety/checkin",
            new { bookingId, shareLocation = false });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        Assert.Empty(Sms.Sent);
        Assert.Empty(Email.Sent);

        var data = Data(await res.Content.ReadAsStringAsync());
        Assert.False(data.GetProperty("contactNotified").GetBoolean());
    }
}
