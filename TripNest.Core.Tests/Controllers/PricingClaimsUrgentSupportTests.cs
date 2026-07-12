using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for the last competitor-gap trio: dynamic pricing (opt-in demand-based rates, demand
/// events, host bounds, quote/booking consistency), damage-protection claims (filing window, one
/// per booking, tenant response, admin decision → fee-free payout), and urgent support (queue-
/// jumping ticket, emergency admin paging, SLA acknowledgement).
/// </summary>
public class PricingClaimsUrgentSupportTests : TestBase
{
    private static readonly byte[] PngPixel = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    // ------------------------------------------------------------ dynamic pricing

    [Fact]
    public async Task DynamicPricing_EmptyCityDiscounts_EventsUplift_BoundsClamp_BookingMatchesQuote()
    {
        // Unique city so other tests' listings never pollute the occupancy signal.
        var city = $"Tamale{Guid.NewGuid():N}";
        var (propertyId, _) = await CreateActivePropertyAsync(location: $"{city}, Ghana");

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.PricingSettings.Add(new PricingSettings
            {
                PropertyId = propertyId,
                BaseRate = 100m,
                DynamicPricingEnabled = true
            });
            await db.SaveChangesAsync();
        }

        // Far-future weekday nights, empty city, no events: only the 0.9x occupancy floor applies.
        var monday = NextMonday(40);
        var quiet = await DataOf(await _httpClient.GetAsync(
            $"/api/properties/{propertyId}/quote?checkIn={monday:yyyy-MM-dd}&checkOut={monday.AddDays(2):yyyy-MM-dd}"));
        Assert.Equal(180m, quiet.GetProperty("total").GetDecimal()); // 2 x (100 x 0.9)

        // An admin demand event covering the dates lifts the rate: 100 x 0.9 x 1.5 = 135/night.
        await LoginAsNewAdminAsync();
        var created = await _httpClient.PostAsJsonAsync("/api/admin/demand-events", new
        {
            name = "Damba Festival",
            location = city,
            startDate = monday.ToString("yyyy-MM-dd"),
            endDate = monday.AddDays(3).ToString("yyyy-MM-dd"),
            upliftPercent = 50
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        ClearAuth();
        var lifted = await DataOf(await _httpClient.GetAsync(
            $"/api/properties/{propertyId}/quote?checkIn={monday:yyyy-MM-dd}&checkOut={monday.AddDays(2):yyyy-MM-dd}"));
        Assert.Equal(270m, lifted.GetProperty("total").GetDecimal());

        // Host floor wins over the demand discount: min 100 => never below base.
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var settings = await db.PricingSettings.FirstAsync(s => s.PropertyId == propertyId);
            settings.MinNightlyRate = 100m;
            settings.MaxNightlyRate = 120m; // and a tight ceiling clamps the event uplift
            await db.SaveChangesAsync();
        }
        var clamped = await DataOf(await _httpClient.GetAsync(
            $"/api/properties/{propertyId}/quote?checkIn={monday:yyyy-MM-dd}&checkOut={monday.AddDays(2):yyyy-MM-dd}"));
        Assert.Equal(240m, clamped.GetProperty("total").GetDecimal()); // 2 x 120 ceiling

        // The booking charges exactly what the quote showed.
        await RegisterAndLoginAsync(UserRole.Tenant);
        var booked = await DataOf(await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = monday.ToString("yyyy-MM-dd"),
            checkOutDate = monday.AddDays(2).ToString("yyyy-MM-dd"),
            guests = 1
        }));
        Assert.Equal(240m, booked.GetProperty("totalAmount").GetDecimal());
    }

    [Fact]
    public async Task DynamicPricing_DisabledListing_Unaffected()
    {
        var (propertyId, _) = await CreateActivePropertyAsync();
        var monday = NextMonday(40);
        var quote = await DataOf(await _httpClient.GetAsync(
            $"/api/properties/{propertyId}/quote?checkIn={monday:yyyy-MM-dd}&checkOut={monday.AddDays(2):yyyy-MM-dd}"));
        Assert.Equal(200m, quote.GetProperty("total").GetDecimal()); // plain 2 x 100
    }

    // ------------------------------------------------------------ damage claims

    [Fact]
    public async Task DamageClaim_FullLifecycle_ApprovalPaysFeeFreePayout()
    {
        var (bookingId, landlordToken, tenantToken) = await CreateCheckedOutBookingAsync(daysSinceCheckout: 3);

        // Landlord files with photo evidence.
        UseToken(landlordToken);
        var form = ClaimForm(bookingId, 300m, "Broken glass door and stained sofa");
        var filed = await _httpClient.PostAsync("/api/claims", form);
        var body = await filed.Content.ReadAsStringAsync();
        Assert.True(filed.StatusCode == HttpStatusCode.Created, body);
        var claimId = JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("claimId").GetString()!;

        // Second claim on the same booking is refused.
        Assert.Equal(HttpStatusCode.Conflict,
            (await _httpClient.PostAsync("/api/claims", ClaimForm(bookingId, 100m, "more damage"))).StatusCode);

        // Tenant responds once.
        UseToken(tenantToken);
        var respond = await _httpClient.PostAsJsonAsync($"/api/claims/{claimId}/respond",
            new { response = "The door was already cracked at check-in." });
        Assert.Equal(HttpStatusCode.OK, respond.StatusCode);

        // A stranger can't see the claim.
        await RegisterAndLoginAsync(UserRole.Tenant);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _httpClient.GetAsync($"/api/claims/booking/{bookingId}")).StatusCode);

        // Admin approves at a reduced amount — the payout is created immediately, fee-free.
        await LoginAsNewAdminAsync();
        var approved = await DataOf(await _httpClient.PostAsJsonAsync($"/api/claims/{claimId}/approve",
            new { approvedAmount = 200m, note = "Sofa pre-existing per photos" }));
        Assert.Equal((int)DamageClaimStatus.Approved, approved.GetProperty("status").GetInt32());
        Assert.Equal(200m, approved.GetProperty("approvedAmount").GetDecimal());

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payout = db.Payouts.Single(p => p.DamageClaimId == claimId);
        Assert.Equal(200m, payout.Amount);
        Assert.Equal(0m, payout.FeeAmount);
        Assert.Null(payout.EscrowId);
    }

    [Fact]
    public async Task DamageClaim_OutsideFilingWindow_Rejected()
    {
        var (bookingId, landlordToken, _) = await CreateCheckedOutBookingAsync(daysSinceCheckout: 20);
        UseToken(landlordToken);
        var filed = await _httpClient.PostAsync("/api/claims", ClaimForm(bookingId, 300m, "Too late"));
        Assert.Equal(HttpStatusCode.BadRequest, filed.StatusCode);
    }

    // ------------------------------------------------------------ urgent support

    [Fact]
    public async Task UrgentHelp_PagesAdmins_JumpsQueue_AndTracksFirstResponse()
    {
        // An admin must exist to be paged (and needs a phone/email for the emergency channel).
        await LoginAsNewAdminAsync();

        var (guestId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        var Email = _fixture.Services.GetRequiredService<RecordingEmailSender>();
        Email.Sent.Clear();

        var res = await DataOf(await _httpClient.PostAsJsonAsync("/api/safety/urgent",
            new { message = "I'm locked out at night and the host is unreachable" }));
        var ticketId = res.GetProperty("ticketId").GetString()!;
        Assert.Equal(15, res.GetProperty("promisedResponseMinutes").GetInt32());

        // The admin was paged through the emergency channel (opt-out bypassing NotifyAsync).
        Assert.Contains(Email.Sent, m => m.Body.Contains("locked out"));

        // Urgent tickets jump the admin queue.
        await LoginAsNewAdminAsync();
        var tickets = await DataOf(await _httpClient.GetAsync("/api/admin/support-tickets"));
        var first = tickets.GetProperty("items").EnumerateArray().First();
        Assert.True(first.GetProperty("isUrgent").GetBoolean());
        Assert.Equal(ticketId, first.GetProperty("ticketId").GetString());

        // Acknowledging stamps the SLA clock (idempotently).
        var ack = await DataOf(await _httpClient.PostAsync($"/api/admin/support-tickets/{ticketId}/ack", null));
        Assert.True(ack.GetProperty("responseSeconds").GetInt32() >= 0);
        var again = await DataOf(await _httpClient.PostAsync($"/api/admin/support-tickets/{ticketId}/ack", null));
        Assert.Equal(ack.GetProperty("firstRespondedAt").GetDateTime(), again.GetProperty("firstRespondedAt").GetDateTime());
        Assert.NotNull(guestId);
    }

    // ------------------------------------------------------------------ helpers

    private static MultipartFormDataContent ClaimForm(string bookingId, decimal amount, string description)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(bookingId), "bookingId" },
            { new StringContent(amount.ToString(System.Globalization.CultureInfo.InvariantCulture)), "amount" },
            { new StringContent(description), "description" }
        };
        var photo = new ByteArrayContent(PngPixel);
        photo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(photo, "photos", "damage.png");
        return form;
    }

    private async Task<(string BookingId, string LandlordToken, string TenantToken)> CreateCheckedOutBookingAsync(int daysSinceCheckout)
    {
        var (propertyId, landlordToken) = await CreateActivePropertyAsync();
        var (tenantId, tenantToken) = await RegisterAndLoginAsync(UserRole.Tenant);

        string bookingId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var booking = new Booking
            {
                TenantId = tenantId,
                PropertyId = propertyId,
                CheckInDate = DateTime.UtcNow.Date.AddDays(-daysSinceCheckout - 3),
                CheckOutDate = DateTime.UtcNow.Date.AddDays(-daysSinceCheckout),
                TotalAmount = 300m,
                Status = BookingStatus.CheckedOut
            };
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();
            bookingId = booking.Id;
        }
        return (bookingId, landlordToken, tenantToken);
    }

    private async Task<(string PropertyId, string LandlordToken)> CreateActivePropertyAsync(string location = "Accra, Ghana")
    {
        var (landlordId, token) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(landlordId);

        var res = await _httpClient.PostAsJsonAsync("/api/properties", new CreatePropertyRequest
        {
            Title = "Feature Test Listing",
            Description = "For pricing/claims/urgent tests",
            Location = location,
            Latitude = 9.4,
            Longitude = -0.85,
            Bedrooms = 2,
            Bathrooms = 1,
            MonthlyRent = 3000m,
            DailyRate = 100m,
            PropertyType = "Apartment",
            StayType = StayType.ShortTerm,
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
        return (propertyId, token);
    }

    private async Task LoginAsNewAdminAsync()
    {
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await RegisterAndLoginAsync(UserRole.Tenant, email);
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        }
        var res = await _httpClient.PostAsJsonAsync("/api/auth/login", new { email, password = "Password@123" });
        var data = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        UseToken(data.GetProperty("accessToken").GetString()!);
    }

    private void UseToken(string token) =>
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    private static DateTime NextMonday(int minDaysOut)
    {
        var day = DateTime.UtcNow.Date.AddDays(minDaysOut);
        while (day.DayOfWeek != DayOfWeek.Monday) day = day.AddDays(1);
        return day;
    }

    private static async Task<JsonElement> DataOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data");
    }
}
