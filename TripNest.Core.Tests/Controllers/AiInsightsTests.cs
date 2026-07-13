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
/// Coverage for the AI-assist surface (all driven by the stub AI client): review summaries,
/// natural-language search, the listing quality coach, admin claim briefs, agreement summaries,
/// roommate explanations, maintenance triage (with label whitelisting), the walkthrough
/// reviewer check, and the switched-off behavior when no provider is configured.
/// </summary>
public class AiInsightsTests : TestBase
{
    private StubAiClient Ai => _fixture.Services.GetRequiredService<StubAiClient>();

    private static readonly byte[] PngPixel = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [Fact]
    public async Task ReviewSummary_SummarisesThemes_AndNeedsTwoReviews()
    {
        var (propertyId, landlordId) = await CreateActivePropertyAsync();

        // One review is not enough.
        await SeedReviewAsync(propertyId, landlordId, 5, "Spotless and quiet");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _httpClient.GetAsync($"/api/reviews/property/{propertyId}/summary")).StatusCode);

        await SeedReviewAsync(propertyId, landlordId, 4, "Great water pressure, some street noise");
        Ai.NextCompletion = """{"summary":"Guests love the cleanliness.","positives":["Spotless","Water pressure"],"negatives":["Street noise"]}""";

        var summary = await DataOf(await _httpClient.GetAsync($"/api/reviews/property/{propertyId}/summary"));
        Assert.Equal("Guests love the cleanliness.", summary.GetProperty("summary").GetString());
        Assert.Equal(2, summary.GetProperty("reviewCount").GetInt32());
        Assert.Contains("Street noise", summary.GetProperty("negatives").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task NaturalSearch_ParsesPhrase_AndRunsTheRealSearch()
    {
        var (accraId, _) = await CreateActivePropertyAsync(location: "Accra, Ghana");
        var (kumasiId, _) = await CreateActivePropertyAsync(location: "Kumasi, Ghana");

        Ai.NextCompletion = """{"location":"Accra","maxPrice":500}""";
        var result = await DataOf(await _httpClient.GetAsync("/api/properties/search/natural?q=somewhere%20cheap%20in%20accra"));

        Assert.Equal("Accra", result.GetProperty("criteria").GetProperty("location").GetString());
        var ids = result.GetProperty("results").EnumerateArray()
            .Select(p => p.GetProperty("propertyId").GetString()).ToList();
        Assert.Contains(accraId, ids);
        Assert.DoesNotContain(kumasiId, ids);
    }

    [Fact]
    public async Task QualityReport_OwnerOnly_ScoreIsDeterministic()
    {
        var (propertyId, _) = await CreateActivePropertyAsync();

        Ai.NextCompletion = """{"suggestions":["Add photos of the kitchen"],"photoNotes":[]}""";
        var report = await DataOf(await _httpClient.GetAsync($"/api/properties/{propertyId}/quality-report"));
        Assert.InRange(report.GetProperty("score").GetInt32(), 0, 100);
        Assert.True(report.GetProperty("checks").GetArrayLength() >= 4);
        Assert.Contains("Add photos of the kitchen",
            report.GetProperty("aiSuggestions").EnumerateArray().Select(x => x.GetString()));

        // Not the owner → 403.
        await RegisterAndLoginAsync(UserRole.Tenant);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _httpClient.GetAsync($"/api/properties/{propertyId}/quality-report")).StatusCode);
    }

    [Fact]
    public async Task ClaimBrief_AdminOnly_ReturnsNeutralBrief()
    {
        var claimId = await CreateClaimAsync();

        // The landlord who filed it cannot pull the admin brief.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _httpClient.GetAsync($"/api/claims/{claimId}/brief")).StatusCode);

        await LoginAsNewAdminAsync();
        Ai.NextCompletion = """{"brief":"Host claims a broken door; tenant disputes.","keyPoints":["GHS 300 claimed"],"inconsistencies":[]}""";
        var brief = await DataOf(await _httpClient.GetAsync($"/api/claims/{claimId}/brief"));
        Assert.Contains("broken door", brief.GetProperty("brief").GetString());
        Assert.Contains("verify", brief.GetProperty("disclaimer").GetString()!.ToLower());
    }

    [Fact]
    public async Task AgreementSummary_PartiesOnly_InPlainLanguage()
    {
        var (agreementId, _) = await CreateAgreementAsync();

        Ai.NextCompletion = """{"summary":"You rent the flat for three nights.","keyTerms":["GHS 300 total"],"yourObligations":["Pay on time"]}""";
        var summary = await DataOf(await _httpClient.GetAsync($"/api/agreements/{agreementId}/summary"));
        Assert.Contains("three nights", summary.GetProperty("summary").GetString());

        // A stranger is not a party.
        await RegisterAndLoginAsync(UserRole.Tenant);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _httpClient.GetAsync($"/api/agreements/{agreementId}/summary")).StatusCode);
    }

    [Fact]
    public async Task RoommateExplanation_NeedsOwnProfile_ThenExplains()
    {
        var (mateId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await _httpClient.PutAsJsonAsync("/api/roommates/me", RoommateProfileBody());

        var (_, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        // No own profile yet → 400.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _httpClient.GetAsync($"/api/roommates/matches/{mateId}/explanation")).StatusCode);

        await _httpClient.PutAsJsonAsync("/api/roommates/me", RoommateProfileBody());
        Ai.NextCompletion = """{"explanation":"You both keep early hours and similar budgets.","sharedTraits":["Early risers"],"considerations":["Agree on guests policy"]}""";
        var explanation = await DataOf(await _httpClient.GetAsync($"/api/roommates/matches/{mateId}/explanation"));
        Assert.Contains("early hours", explanation.GetProperty("explanation").GetString());
    }

    [Fact]
    public async Task MaintenanceTriage_LabelsStored_HallucinationsWhitelistedOut()
    {
        var (propertyId, _) = await CreateActivePropertyAsync();
        await RegisterAndLoginAsync(UserRole.Tenant);

        Ai.NextCompletion = """{"urgency":"High","category":"Plumbing"}""";
        var report = await DataOf(await _httpClient.PostAsJsonAsync("/api/maintenance",
            new { propertyId, description = "Burst pipe flooding the kitchen", category = "Plumbing", priority = "High" }));
        Assert.Equal("High", report.GetProperty("triageUrgency").GetString());
        Assert.Equal("Plumbing", report.GetProperty("triageCategory").GetString());

        // A label outside the whitelist must never reach the database.
        Ai.NextCompletion = """{"urgency":"Catastrophic","category":"Sorcery"}""";
        var weird = await DataOf(await _httpClient.PostAsJsonAsync("/api/maintenance",
            new { propertyId, description = "Strange noises at night", category = "Other", priority = "Low" }));
        Assert.Equal(JsonValueKind.Null, weird.GetProperty("triageUrgency").ValueKind);
        Assert.Equal(JsonValueKind.Null, weird.GetProperty("triageCategory").ValueKind);
    }

    [Fact]
    public async Task WalkthroughAiCheck_ReviewerRolesOnly_ChecksListingPhotos()
    {
        var (propertyId, _) = await CreateActivePropertyAsync(uploadPhoto: true);

        // Landlords/tenants can't run the reviewer assist.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _httpClient.GetAsync($"/api/properties/{propertyId}/walkthrough/ai-check")).StatusCode);

        await LoginAsNewAdminAsync();
        Ai.NextCompletion = """{"consistent":true,"observations":["Interior matches a 2-bed apartment"],"redFlags":[]}""";
        var check = await DataOf(await _httpClient.GetAsync($"/api/properties/{propertyId}/walkthrough/ai-check"));
        Assert.True(check.GetProperty("photosConsistentWithListing").GetBoolean());
        Assert.Contains("human", check.GetProperty("disclaimer").GetString()!.ToLower());
    }

    [Fact]
    public async Task Unconfigured_AiFeaturesSwitchOffWithFriendly400()
    {
        var (propertyId, landlordId) = await CreateActivePropertyAsync();
        await SeedReviewAsync(propertyId, landlordId, 5, "Nice");
        await SeedReviewAsync(propertyId, landlordId, 4, "Fine");

        Ai.Configured = false;
        try
        {
            var res = await _httpClient.GetAsync($"/api/reviews/property/{propertyId}/summary");
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Contains("not configured", await res.Content.ReadAsStringAsync());
        }
        finally
        {
            Ai.Configured = true;
        }
    }

    // ------------------------------------------------------------------ helpers

    private static object RoommateProfileBody() => new
    {
        preferredLocation = "Accra, Ghana",
        monthlyBudget = 700,
        university = "University of Ghana",
        smokes = false, okWithSmoker = false, hasPets = false, okWithPets = true,
        nightOwl = false, cleanlinessLevel = 4, isVisible = true
    };

    private async Task SeedReviewAsync(string propertyId, string revieweeId, int rating, string comment)
    {
        var (reviewerId, token) = await RegisterAndLoginAsync(UserRole.Tenant);
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Reviews.Add(new Review
        {
            ReviewerId = reviewerId,
            RevieweeId = revieweeId,
            PropertyId = propertyId,
            Rating = rating,
            Comment = comment
        });
        await db.SaveChangesAsync();
        _ = token;
    }

    private async Task<string> CreateClaimAsync()
    {
        var (propertyId, landlordToken) = await CreateActivePropertyWithTokenAsync();
        var (tenantId, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        string bookingId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var booking = new Booking
            {
                TenantId = tenantId, PropertyId = propertyId,
                CheckInDate = DateTime.UtcNow.Date.AddDays(-6),
                CheckOutDate = DateTime.UtcNow.Date.AddDays(-3),
                TotalAmount = 300m, Status = BookingStatus.CheckedOut
            };
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();
            bookingId = booking.Id;
        }

        UseToken(landlordToken);
        var form = new MultipartFormDataContent
        {
            { new StringContent(bookingId), "bookingId" },
            { new StringContent("300"), "amount" },
            { new StringContent("Broken glass door"), "description" }
        };
        var filed = await _httpClient.PostAsync("/api/claims", form);
        var body = await filed.Content.ReadAsStringAsync();
        Assert.True(filed.StatusCode == HttpStatusCode.Created, body);
        return JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("claimId").GetString()!;
    }

    private async Task<(string AgreementId, string TenantId)> CreateAgreementAsync()
    {
        var (propertyId, _) = await CreateActivePropertyWithTokenAsync();
        var (tenantId, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        var checkIn = DateTime.UtcNow.Date.AddDays(10);
        var booked = await DataOf(await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = checkIn.ToString("yyyy-MM-dd"),
            checkOutDate = checkIn.AddDays(3).ToString("yyyy-MM-dd"),
            guests = 1
        }));
        var bookingId = booked.GetProperty("bookingId").GetString()!;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Bookings.FindAsync(bookingId))!.Status = BookingStatus.Confirmed;
            await db.SaveChangesAsync();
        }

        var agreement = await DataOf(await _httpClient.PostAsJsonAsync("/api/agreements", new { bookingId }));
        return (agreement.GetProperty("agreementId").GetString()!, tenantId);
    }

    private async Task<(string PropertyId, string LandlordId)> CreateActivePropertyAsync(
        string location = "Accra, Ghana", bool uploadPhoto = false)
    {
        var (propertyId, landlordId, _) = await CreatePropertyCoreAsync(location, uploadPhoto);
        return (propertyId, landlordId);
    }

    private async Task<(string PropertyId, string LandlordToken)> CreateActivePropertyWithTokenAsync()
    {
        var (propertyId, _, token) = await CreatePropertyCoreAsync("Accra, Ghana", uploadPhoto: false);
        return (propertyId, token);
    }

    private async Task<(string PropertyId, string LandlordId, string Token)> CreatePropertyCoreAsync(string location, bool uploadPhoto)
    {
        var (landlordId, token) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(landlordId);

        var res = await _httpClient.PostAsJsonAsync("/api/properties", new CreatePropertyRequest
        {
            Title = "AI Test Listing",
            Description = "Bright, quiet, close to transport and shops.",
            Location = location,
            Latitude = 5.6, Longitude = -0.19,
            Bedrooms = 2, Bathrooms = 1,
            MonthlyRent = 2500m, DailyRate = 100m,
            PropertyType = "Apartment", StayType = StayType.ShortTerm,
            CancellationPolicy = CancellationPolicy.Moderate,
            Amenities = "WiFi,Kitchen,AC"
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var propertyId = JsonDocument.Parse(await res.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("propertyId").GetString()!;

        if (uploadPhoto)
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(PngPixel);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            form.Add(file, "files", "room.png");
            var photo = await _httpClient.PostAsync($"/api/properties/{propertyId}/photos", form);
            Assert.True(photo.IsSuccessStatusCode, await photo.Content.ReadAsStringAsync());
        }

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Properties.FindAsync(propertyId))!.Status = PropertyStatus.Active;
        await db.SaveChangesAsync();
        return (propertyId, landlordId, token);
    }

    private async Task LoginAsNewAdminAsync()
    {
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await RegisterAndLoginAsync(UserRole.Tenant, email);
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = db.Users.First(u => u.Email == email);
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

    private static async Task<JsonElement> DataOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data");
    }
}
