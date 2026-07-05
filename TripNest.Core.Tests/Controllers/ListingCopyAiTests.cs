using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// AI listing copy generation: owner-only, verified-host-gated, and gracefully disabled when no
/// AI key is configured — the same degradation contract as the SMS/email/payment integrations.
/// </summary>
public class ListingCopyAiTests : TestBase
{
    [Fact]
    public async Task Owner_GetsSuggestion_WithTitleDescriptionAndHighlights()
    {
        var (ownerId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(ownerId);
        var propertyId = await SeedPropertyAsync(ownerId);

        var stub = _fixture.Services.GetRequiredService<StubAiClient>();
        stub.Configured = true;

        var res = await _httpClient.PostAsync($"/api/properties/{propertyId}/generate-copy", null);
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.OK, $"Expected OK but got {res.StatusCode}: {body}");

        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");
        Assert.False(string.IsNullOrEmpty(data.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrEmpty(data.GetProperty("description").GetString()));
        Assert.True(data.GetProperty("highlights").GetArrayLength() >= 1);

        // The service actually asked the AI client about THIS property.
        Assert.Contains(stub.Requests, r => r.PropertyId == propertyId);
    }

    [Fact]
    public async Task NonOwner_IsRejected()
    {
        var (ownerId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        var propertyId = await SeedPropertyAsync(ownerId);

        // A different verified landlord must not be able to generate copy for someone else's listing.
        var (intruderId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(intruderId);

        var res = await _httpClient.PostAsync($"/api/properties/{propertyId}/generate-copy", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task UnverifiedHost_IsBlockedByVerificationGate()
    {
        var (ownerId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        var propertyId = await SeedPropertyAsync(ownerId);
        // Owner is NOT marked verified — [RequireVerified] must 403 before any AI work happens.

        var res = await _httpClient.PostAsync($"/api/properties/{propertyId}/generate-copy", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task WhenAiNotConfigured_Returns400WithClearMessage()
    {
        var (ownerId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(ownerId);
        var propertyId = await SeedPropertyAsync(ownerId);

        var stub = _fixture.Services.GetRequiredService<StubAiClient>();
        stub.Configured = false;

        var res = await _httpClient.PostAsync($"/api/properties/{propertyId}/generate-copy", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var message = JsonDocument.Parse(await res.Content.ReadAsStringAsync())
            .RootElement.GetProperty("message").GetString();
        Assert.Contains("not configured", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenProviderFails_Returns400_NeverA500()
    {
        var (ownerId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(ownerId);
        var propertyId = await SeedPropertyAsync(ownerId);

        // Configured but the provider call yields nothing (timeout, refusal, malformed output).
        var stub = _fixture.Services.GetRequiredService<StubAiClient>();
        stub.Configured = true;
        stub.NextSuggestion = null;

        var res = await _httpClient.PostAsync($"/api/properties/{propertyId}/generate-copy", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    private async Task<string> SeedPropertyAsync(string ownerId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = new Property
        {
            UserId = ownerId,
            Title = "2 bedroom apartment",
            Description = "apartment in tarkwa",
            Location = "University Area, Tarkwa",
            Latitude = 5.3,
            Longitude = -1.99,
            Bedrooms = 2,
            Bathrooms = 1,
            MonthlyRent = 900m,
            DailyRate = 45m,
            PropertyType = "Apartment",
            Amenities = "WiFi, parking, backup water",
        };
        db.Set<Property>().Add(property);
        await db.SaveChangesAsync();
        return property.Id;
    }
}
