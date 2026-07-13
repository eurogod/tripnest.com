using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for the two "warning" AI features, now completed: read-time cached notification
/// translation into the user's PreferredLanguage (write path stays English, AI only on read),
/// and walkthrough video-frame analysis with graceful fallback to photos when ffmpeg is absent.
/// </summary>
public class AiMultilingualAndVideoTests : TestBase
{
    private StubAiClient Ai => _fixture.Services.GetRequiredService<StubAiClient>();
    private StubVideoFrameExtractor Video => _fixture.Services.GetRequiredService<StubVideoFrameExtractor>();

    private static readonly byte[] PngPixel = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    // ---------------------------------------------------------------- #4 translation

    [Fact]
    public async Task Notifications_TranslatedIntoReaderLanguage_EnglishUntouched()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await SeedNotificationAsync(userId, "Rent received", "Rent of GHS 900 was paid.");

        // English reader: no translation, verbatim (and the stub AI is not consulted).
        Ai.Completions.Clear();
        var english = await NotificationsAsync();
        Assert.Equal("Rent received", english[0].GetProperty("title").GetString());
        Assert.Empty(Ai.Completions);

        // Switch the reader to Twi → the list renders translated text at read time.
        await SetLanguageAsync(userId, Language.Twi);
        Ai.NextCompletion = """{"title":"Woagye ka","message":"Woatua ka GHS 900."}""";
        var twi = await NotificationsAsync();
        Assert.Equal("Woagye ka", twi[0].GetProperty("title").GetString());
        Assert.Single(Ai.Completions);

        // Second fetch is served from cache — no further AI call.
        Ai.Completions.Clear();
        var again = await NotificationsAsync();
        Assert.Equal("Woagye ka", again[0].GetProperty("title").GetString());
        Assert.Empty(Ai.Completions);
    }

    [Fact]
    public async Task Notifications_TranslationFailure_FallsBackToEnglish()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await SeedNotificationAsync(userId, "Booking confirmed", "Your stay is confirmed.");
        await SetLanguageAsync(userId, Language.French);

        // Provider returns nothing usable → the original English text is shown, never an error.
        Ai.NextCompletion = null;
        var list = await NotificationsAsync();
        Assert.Equal("Booking confirmed", list[0].GetProperty("title").GetString());
    }

    // ---------------------------------------------------------------- #9 video frames

    [Fact]
    public async Task WalkthroughCheck_UsesVideoFrames_WhenAvailable()
    {
        var propertyId = await CreatePropertyWithVideoAsync();

        Video.Available = true;
        Video.FrameCount = 3;
        Ai.NextCompletion = """{"consistent":true,"observations":["Walkthrough matches a 2-bed flat"],"redFlags":[]}""";

        await LoginAsNewAdminAsync();
        var check = await DataOf(await _httpClient.GetAsync($"/api/properties/{propertyId}/walkthrough/ai-check"));
        Assert.Equal(3, check.GetProperty("videoFramesAnalysed").GetInt32());
        Assert.True(check.GetProperty("photosConsistentWithListing").GetBoolean());
    }

    [Fact]
    public async Task WalkthroughCheck_FallsBackToPhotos_WhenFfmpegUnavailable()
    {
        var propertyId = await CreatePropertyWithVideoAsync(withPhoto: true);

        Video.Available = false; // ffmpeg not installed
        Ai.NextCompletion = """{"consistent":true,"observations":["Photos look like a real flat"],"redFlags":[]}""";

        await LoginAsNewAdminAsync();
        var check = await DataOf(await _httpClient.GetAsync($"/api/properties/{propertyId}/walkthrough/ai-check"));
        Assert.Equal(0, check.GetProperty("videoFramesAnalysed").GetInt32());
    }

    // ---------------------------------------------------------------- helpers

    private async Task<List<JsonElement>> NotificationsAsync()
    {
        var data = await DataOf(await _httpClient.GetAsync("/api/notifications/mine"));
        return data.GetProperty("items").EnumerateArray().ToList();
    }

    private async Task SeedNotificationAsync(string userId, string title, string message)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Notifications.Add(new Notification { UserId = userId, Title = title, Message = message });
        await db.SaveChangesAsync();
    }

    private async Task SetLanguageAsync(string userId, Language language)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.FindAsync(userId))!.PreferredLanguage = language;
        await db.SaveChangesAsync();
    }

    private async Task<string> CreatePropertyWithVideoAsync(bool withPhoto = false)
    {
        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(landlordId);
        var res = await _httpClient.PostAsJsonAsync("/api/properties", new CreatePropertyRequest
        {
            Title = "Video Test Listing", Description = "Bright and close to campus.",
            Location = "Accra, Ghana", Latitude = 5.6, Longitude = -0.19,
            Bedrooms = 2, Bathrooms = 1, MonthlyRent = 2500m, DailyRate = 100m,
            PropertyType = "Apartment", StayType = StayType.ShortTerm,
            CancellationPolicy = CancellationPolicy.Moderate, Amenities = "WiFi,Kitchen"
        });
        var propertyId = JsonDocument.Parse(await res.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("propertyId").GetString()!;

        if (withPhoto)
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(PngPixel);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            form.Add(file, "files", "room.png");
            await _httpClient.PostAsync($"/api/properties/{propertyId}/photos", form);
        }

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await db.Properties.FindAsync(propertyId);
        property!.Status = PropertyStatus.Active;
        property.WalkthroughVideoPath = "walkthroughs/test-video.mp4";
        await db.SaveChangesAsync();
        return propertyId;
    }

    private async Task LoginAsNewAdminAsync()
    {
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await RegisterAndLoginAsync(UserRole.Tenant, email);
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.First(u => u.Email == email).Role = UserRole.Admin;
            await db.SaveChangesAsync();
        }
        var res = await _httpClient.PostAsJsonAsync("/api/auth/login", new { email, password = "Password@123" });
        var data = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
    }

    private static async Task<JsonElement> DataOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data");
    }
}
