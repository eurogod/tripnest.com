using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for student verification: academic-domain gating, the OTP round trip via the
/// recording email sender, status expiry, and the student discount on Student-stayType quotes.
/// </summary>
public class StudentVerificationTests : TestBase
{
    private RecordingEmailSender Email => _fixture.Services.GetRequiredService<RecordingEmailSender>();

    [Fact]
    public async Task SendOtp_RejectsNonAcademicDomains()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);

        foreach (var bad in new[] { "me@gmail.com", "me@yahoo.co.uk", "not-an-email" })
        {
            var res = await _httpClient.PostAsJsonAsync("/api/auth/student/send-otp", new { studentEmail = bad });
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }
        Assert.Empty(Email.Sent);
    }

    [Fact]
    public async Task SendThenVerify_AcademicEmail_UnlocksActiveStudentStatus()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        Email.Sent.Clear();

        var send = await _httpClient.PostAsJsonAsync("/api/auth/student/send-otp",
            new { studentEmail = "kwame@st.ug.edu.gh" });
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);

        // The code went to the STUDENT mailbox, not the account email.
        var sent = Assert.Single(Email.Sent);
        Assert.Equal("kwame@st.ug.edu.gh", sent.To);
        var code = Regex.Match(sent.Body, @"\d{6}").Value;

        // A wrong code is rejected without verifying.
        var wrong = await _httpClient.PostAsJsonAsync("/api/auth/student/verify-otp", new { code = "000000" });
        if (code != "000000")
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var verified = await DataOf(await _httpClient.PostAsJsonAsync("/api/auth/student/verify-otp", new { code }));
        Assert.True(verified.GetProperty("isVerifiedStudent").GetBoolean());
        Assert.Equal("kwame@st.ug.edu.gh", verified.GetProperty("studentEmail").GetString());

        var status = await DataOf(await _httpClient.GetAsync("/api/auth/student"));
        Assert.True(status.GetProperty("isVerifiedStudent").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, status.GetProperty("expiresAt").ValueKind);
    }

    [Fact]
    public async Task StudentDiscount_AppliesOnStudentListings_NotElsewhere_AndExpires()
    {
        var studentPropertyId = await CreateActivePropertyAsync(StayType.Student);
        var shortTermPropertyId = await CreateActivePropertyAsync(StayType.ShortTerm);

        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        Email.Sent.Clear();
        await _httpClient.PostAsJsonAsync("/api/auth/student/send-otp", new { studentEmail = "ama@knust.edu.gh" });
        var code = Regex.Match(Assert.Single(Email.Sent).Body, @"\d{6}").Value;
        await _httpClient.PostAsJsonAsync("/api/auth/student/verify-otp", new { code });

        // 2 weekday nights x 100 = 200; 5% student discount => 10 off on the Student listing.
        var monday = NextMonday();
        var studentQuote = await DataOf(await _httpClient.GetAsync(
            $"/api/properties/{studentPropertyId}/quote?checkIn={monday:yyyy-MM-dd}&checkOut={monday.AddDays(2):yyyy-MM-dd}"));
        Assert.Equal(10m, studentQuote.GetProperty("loyaltyDiscount").GetDecimal());
        Assert.Equal(190m, studentQuote.GetProperty("total").GetDecimal());

        // No student discount on a short-term listing (and no loyalty tier yet either).
        var shortQuote = await DataOf(await _httpClient.GetAsync(
            $"/api/properties/{shortTermPropertyId}/quote?checkIn={monday:yyyy-MM-dd}&checkOut={monday.AddDays(2):yyyy-MM-dd}"));
        Assert.Equal(0m, shortQuote.GetProperty("loyaltyDiscount").GetDecimal());
        Assert.Equal(200m, shortQuote.GetProperty("total").GetDecimal());

        // Graduated: verification older than the validity window no longer discounts.
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(userId);
            user!.StudentVerifiedAt = DateTime.UtcNow.AddDays(-400);
            await db.SaveChangesAsync();
        }

        var expiredQuote = await DataOf(await _httpClient.GetAsync(
            $"/api/properties/{studentPropertyId}/quote?checkIn={monday:yyyy-MM-dd}&checkOut={monday.AddDays(2):yyyy-MM-dd}"));
        Assert.Equal(200m, expiredQuote.GetProperty("total").GetDecimal());

        var status = await DataOf(await _httpClient.GetAsync("/api/auth/student"));
        Assert.False(status.GetProperty("isVerifiedStudent").GetBoolean());
    }

    // ------------------------------------------------------------------ helpers

    private async Task<string> CreateActivePropertyAsync(StayType stayType)
    {
        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(landlordId);

        var res = await _httpClient.PostAsJsonAsync("/api/properties", new CreatePropertyRequest
        {
            Title = "Student Hall Room",
            Description = "Close to campus",
            Location = "Accra, Ghana",
            Latitude = 5.65,
            Longitude = -0.19,
            Bedrooms = 1,
            Bathrooms = 1,
            MonthlyRent = 900m,
            DailyRate = 100m,
            PropertyType = "Hostel",
            StayType = stayType,
            CancellationPolicy = CancellationPolicy.Flexible
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

    private static DateTime NextMonday()
    {
        var day = DateTime.UtcNow.Date.AddDays(14);
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
