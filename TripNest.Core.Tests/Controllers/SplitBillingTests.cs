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
/// Coverage for unified split billing: share creation with exact-sum rounding, per-member
/// checkouts, confirm-only-when-everyone-paid, the group payment window, and ownership guards.
/// </summary>
public class SplitBillingTests : TestBase
{
    private StubPaymentGateway Gateway => _fixture.Services.GetRequiredService<StubPaymentGateway>();

    [Fact]
    public async Task GroupBooking_SplitsExactly_ConfirmsOnlyWhenEveryonePaid()
    {
        var propertyId = await CreateActivePropertyAsync(dailyRate: 100m);

        // Two co-travellers + the booker; 3 nights x 100 = 300 => 100 each.
        var (mate1Id, _) = await RegisterAndLoginAsync(UserRole.Tenant, email: $"mate1_{Guid.NewGuid():N}@example.com");
        var (mate2Id, _) = await RegisterAndLoginAsync(UserRole.Tenant, email: $"mate2_{Guid.NewGuid():N}@example.com");
        var mate1Email = await EmailOf(mate1Id);
        var mate2Email = await EmailOf(mate2Id);
        var (bookerId, bookerToken) = await RegisterAndLoginAsync(UserRole.Tenant);

        var checkIn = NextMonday(21);
        var booked = await DataOf(await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = checkIn.ToString("yyyy-MM-dd"),
            checkOutDate = checkIn.AddDays(3).ToString("yyyy-MM-dd"),
            guests = 3,
            splitWithEmails = new[] { mate1Email, mate2Email }
        }));
        var bookingId = booked.GetProperty("bookingId").GetString()!;
        var shares = booked.GetProperty("shares").EnumerateArray().ToList();

        Assert.Equal(3, shares.Count);
        Assert.Equal(300m, shares.Sum(s => s.GetProperty("amount").GetDecimal()));

        // Whole-booking checkout is blocked for group bookings.
        var whole = await _httpClient.PostAsJsonAsync("/api/escrow/initiate", new { bookingId });
        Assert.Equal(HttpStatusCode.BadRequest, whole.StatusCode);

        // Everyone pays their own share; the booking must stay Pending until the last one lands.
        var tokens = new Dictionary<string, string>
        {
            [bookerId] = bookerToken,
            [mate1Id] = await LoginAndGetTokenAsync(mate1Email),
            [mate2Id] = await LoginAndGetTokenAsync(mate2Email)
        };

        var paidSoFar = 0;
        foreach (var share in shares)
        {
            var shareId = share.GetProperty("shareId").GetString()!;
            var participantId = share.GetProperty("participantUserId").GetString()!;
            UseToken(tokens[participantId]);

            var initiated = await DataOf(await _httpClient.PostAsync($"/api/bookings/shares/{shareId}/pay", null));
            Assert.False(string.IsNullOrEmpty(initiated.GetProperty("checkoutUrl").GetString()));

            Gateway.VerifyAmount = share.GetProperty("amount").GetDecimal();
            var verified = await DataOf(await _httpClient.PostAsync($"/api/bookings/shares/{shareId}/verify", null));
            Assert.Equal((int)BookingShareStatus.Paid, verified.GetProperty("status").GetInt32());
            paidSoFar++;

            using var scope = _fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var booking = await db.Bookings.FindAsync(bookingId);
            var escrow = db.Escrows.Single(e => e.BookingId == bookingId);
            if (paidSoFar < shares.Count)
            {
                Assert.Equal(BookingStatus.Pending, booking!.Status);
                Assert.Equal(EscrowStatus.Pending, escrow.Status);
            }
            else
            {
                Assert.Equal(BookingStatus.Confirmed, booking!.Status);
                Assert.Equal(EscrowStatus.HeldInEscrow, escrow.Status);
            }
        }
    }

    [Fact]
    public async Task GroupBooking_BookerAbsorbsRounding_SharesSumToTotal()
    {
        var propertyId = await CreateActivePropertyAsync(dailyRate: 100m);
        var (mateId, _) = await RegisterAndLoginAsync(UserRole.Tenant, email: $"mate_{Guid.NewGuid():N}@example.com");
        var mateEmail = await EmailOf(mateId);
        var (_, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        // 1 night x 100 = 100 split 3 ways => 33.33 + 33.33 + booker 33.34.
        var (mate2Id, _) = await RegisterAndLoginAsync(UserRole.Tenant, email: $"mate2_{Guid.NewGuid():N}@example.com");
        var mate2Email = await EmailOf(mate2Id);
        var (bookerId, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        var checkIn = NextMonday(30);
        var booked = await DataOf(await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = checkIn.ToString("yyyy-MM-dd"),
            checkOutDate = checkIn.AddDays(1).ToString("yyyy-MM-dd"),
            guests = 3,
            splitWithEmails = new[] { mateEmail, mate2Email }
        }));

        var shares = booked.GetProperty("shares").EnumerateArray().ToList();
        var bookerShare = shares.Single(s => s.GetProperty("participantUserId").GetString() == bookerId);
        Assert.Equal(33.34m, bookerShare.GetProperty("amount").GetDecimal());
        Assert.Equal(100m, shares.Sum(s => s.GetProperty("amount").GetDecimal()));
    }

    [Fact]
    public async Task GroupBooking_UnknownEmail_Rejected()
    {
        var propertyId = await CreateActivePropertyAsync();
        await RegisterAndLoginAsync(UserRole.Tenant);

        var checkIn = NextMonday(21);
        var res = await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = checkIn.ToString("yyyy-MM-dd"),
            checkOutDate = checkIn.AddDays(2).ToString("yyyy-MM-dd"),
            guests = 2,
            splitWithEmails = new[] { "nobody@nowhere.example" }
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Share_OnlyItsOwnerCanPayIt()
    {
        var (bookingId, shares, _) = await CreateGroupBookingAsync();
        var strangerShare = shares[1].GetProperty("shareId").GetString()!; // co-traveller's share

        // Still authenticated as the booker — paying someone else's share is forbidden.
        var res = await _httpClient.PostAsync($"/api/bookings/shares/{strangerShare}/pay", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.NotNull(bookingId);
    }

    [Fact]
    public async Task ExpiredGroupBooking_CancelsAndRefundsPaidShares()
    {
        var (bookingId, shares, _) = await CreateGroupBookingAsync();

        // The booker pays their share…
        var bookerShare = shares[0];
        var bookerShareId = bookerShare.GetProperty("shareId").GetString()!;
        await DataOf(await _httpClient.PostAsync($"/api/bookings/shares/{bookerShareId}/pay", null));
        Gateway.VerifyAmount = bookerShare.GetProperty("amount").GetDecimal();
        await DataOf(await _httpClient.PostAsync($"/api/bookings/shares/{bookerShareId}/verify", null));

        // …but the group blows the 24h window.
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var booking = await db.Bookings.FindAsync(bookingId);
            booking!.CreatedAt = DateTime.UtcNow.AddHours(-25);
            await db.SaveChangesAsync();
        }

        // Any further pay attempt trips lazy expiry: 400, booking cancelled, paid share refunded.
        var mateShareId = shares[1].GetProperty("shareId").GetString()!;
        UseToken(_mateToken!);
        var late = await _httpClient.PostAsync($"/api/bookings/shares/{mateShareId}/pay", null);
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var booking = await db.Bookings.FindAsync(bookingId);
            Assert.Equal(BookingStatus.Cancelled, booking!.Status);
            var refunded = db.BookingShares.Single(s => s.Id == bookerShareId);
            Assert.Equal(BookingShareStatus.Refunded, refunded.Status);
        }
        Assert.Contains(Gateway.Refunds, r => r.Amount == bookerShare.GetProperty("amount").GetDecimal());
    }

    // ------------------------------------------------------------------ helpers

    private string? _mateToken;

    /// <summary>Group booking of booker + one co-traveller; leaves the client as the booker.</summary>
    private async Task<(string BookingId, List<JsonElement> Shares, string BookerId)> CreateGroupBookingAsync()
    {
        var propertyId = await CreateActivePropertyAsync(dailyRate: 100m);
        var (mateId, _) = await RegisterAndLoginAsync(UserRole.Tenant, email: $"mate_{Guid.NewGuid():N}@example.com");
        var mateEmail = await EmailOf(mateId);
        _mateToken = await LoginAndGetTokenAsync(mateEmail);
        var (bookerId, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        var checkIn = NextMonday(21);
        var booked = await DataOf(await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = checkIn.ToString("yyyy-MM-dd"),
            checkOutDate = checkIn.AddDays(2).ToString("yyyy-MM-dd"),
            guests = 2,
            splitWithEmails = new[] { mateEmail }
        }));

        var shares = booked.GetProperty("shares").EnumerateArray().ToList();
        // Shares are returned booker-first (creation order).
        Assert.Equal(bookerId, shares[0].GetProperty("participantUserId").GetString());
        return (booked.GetProperty("bookingId").GetString()!, shares, bookerId);
    }

    private async Task<string> CreateActivePropertyAsync(decimal dailyRate = 120m)
    {
        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(landlordId);

        var res = await _httpClient.PostAsJsonAsync("/api/properties", new CreatePropertyRequest
        {
            Title = "Group Stay Villa",
            Description = "Sleeps the whole crew",
            Location = "Accra, Ghana",
            Latitude = 5.6,
            Longitude = -0.19,
            Bedrooms = 4,
            Bathrooms = 3,
            MonthlyRent = 6000m,
            DailyRate = dailyRate,
            PropertyType = "Villa",
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
        return propertyId;
    }

    private async Task<string> EmailOf(string userId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Users.FindAsync(userId))!.Email;
    }

    private async Task<string> LoginAndGetTokenAsync(string email)
    {
        var res = await _httpClient.PostAsJsonAsync("/api/auth/login", new { email, password = "Password@123" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
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
