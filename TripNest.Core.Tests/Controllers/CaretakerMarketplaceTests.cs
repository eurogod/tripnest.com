using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Caretakers;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for the caretaker marketplace: self-service directory profile (GET/PUT
/// /api/caretakers/me — previously caretakers could only be seeded, never onboarded),
/// assignment lifecycle against the PropertyCaretakerAssignment table, service-request
/// status transitions, and review → rating aggregation.
/// </summary>
public class CaretakerMarketplaceTests : TestBase
{
    private static UpsertCaretakerProfileRequest ValidProfile() => new()
    {
        Responsibilities = "cleaning, plumbing, garden upkeep",
        Bio = "Reliable East Legon caretaker",
        ServiceArea = "East Legon",
        MonthlyCompensation = 800m
    };

    private async Task<(string UserId, string CaretakerId)> OnboardCaretakerAsync()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Caretaker);
        await MarkUserVerifiedAsync(userId);
        var put = await _httpClient.PutAsJsonAsync("/api/caretakers/me", ValidProfile());
        var body = await put.Content.ReadAsStringAsync();
        Assert.True(put.StatusCode == HttpStatusCode.OK, $"Expected OK but got {put.StatusCode}: {body}");
        var caretakerId = JsonDocument.Parse(body).RootElement
            .GetProperty("data").GetProperty("caretakerId").GetString()!;
        return (userId, caretakerId);
    }

    private async Task<string> SeedPropertyAsync(string ownerId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = new Property
        {
            UserId = ownerId,
            Title = "Caretaker Test Property",
            Description = "d",
            Location = "Accra",
            Latitude = 5.6,
            Longitude = -0.2,
            Bedrooms = 2,
            Bathrooms = 1,
            MonthlyRent = 1000m,
            DailyRate = 40m,
            PropertyType = "Apartment"
        };
        db.Set<Property>().Add(property);
        await db.SaveChangesAsync();
        return property.Id;
    }

    private int CountNotifications(string userId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.Set<Notification>().Count(n => n.UserId == userId);
    }

    [Fact]
    public async Task UpsertProfile_ThenCaretakerAppearsInPublicList_AndFiltersWork()
    {
        var (userId, _) = await OnboardCaretakerAsync();

        var list = await _httpClient.GetAsync("/api/caretakers?area=east+legon&serviceType=plumbing");
        var data = JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.Equal(1, data.GetProperty("items").GetArrayLength());
        Assert.Equal(userId, data.GetProperty("items")[0].GetProperty("userId").GetString());

        var miss = await _httpClient.GetAsync("/api/caretakers?area=kumasi");
        var missData = JsonDocument.Parse(await miss.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.Equal(0, missData.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task GetMyProfile_BeforeCreating_ReturnsNotFound()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Caretaker);
        await MarkUserVerifiedAsync(userId);

        var response = await _httpClient.GetAsync("/api/caretakers/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpsertProfile_UnverifiedCaretaker_IsForbidden()
    {
        await RegisterAndLoginAsync(UserRole.Caretaker);

        var response = await _httpClient.PutAsJsonAsync("/api/caretakers/me", ValidProfile());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssignUnassignLifecycle_TracksAssignmentsAndNotifiesCaretaker()
    {
        var (caretakerUserId, caretakerId) = await OnboardCaretakerAsync();

        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(landlordId);
        var propertyId = await SeedPropertyAsync(landlordId);

        var assign = await _httpClient.PostAsJsonAsync("/api/caretakers/assign",
            new { propertyId, caretakerId });
        var assignBody = await assign.Content.ReadAsStringAsync();
        Assert.True(assign.StatusCode == HttpStatusCode.OK, $"Expected OK but got {assign.StatusCode}: {assignBody}");

        // Re-assigning while the assignment is active conflicts.
        var again = await _httpClient.PostAsJsonAsync("/api/caretakers/assign",
            new { propertyId, caretakerId });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var mine = await _httpClient.GetAsync("/api/caretakers/assignments/mine");
        var mineData = JsonDocument.Parse(await mine.Content.ReadAsStringAsync()).RootElement.GetProperty("data").GetProperty("items");
        Assert.Equal(1, mineData.GetArrayLength());
        Assert.True(mineData[0].GetProperty("isActive").GetBoolean());

        var unassign = await _httpClient.PostAsJsonAsync("/api/caretakers/unassign",
            new { propertyId, caretakerId });
        Assert.Equal(HttpStatusCode.OK, unassign.StatusCode);

        var after = await _httpClient.GetAsync("/api/caretakers/assignments/mine");
        var afterData = JsonDocument.Parse(await after.Content.ReadAsStringAsync()).RootElement.GetProperty("data").GetProperty("items");
        Assert.False(afterData[0].GetProperty("isActive").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, afterData[0].GetProperty("endedAt").ValueKind);

        // No active assignment left to end.
        var repeat = await _httpClient.PostAsJsonAsync("/api/caretakers/unassign",
            new { propertyId, caretakerId });
        Assert.Equal(HttpStatusCode.NotFound, repeat.StatusCode);

        // Assignment + unassignment each notified the caretaker.
        Assert.True(CountNotifications(caretakerUserId) >= 2);
    }

    [Fact]
    public async Task Assign_PropertyNotOwnedByCaller_IsForbidden()
    {
        var (_, caretakerId) = await OnboardCaretakerAsync();

        var propertyId = await SeedPropertyAsync("someone-else");
        var (landlordId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(landlordId);

        var response = await _httpClient.PostAsJsonAsync("/api/caretakers/assign",
            new { propertyId, caretakerId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ServiceRequestLifecycle_TransitionsAreRoleGated_AndReviewAggregates()
    {
        var (caretakerUserId, caretakerId) = await OnboardCaretakerAsync();
        var propertyId = await SeedPropertyAsync("some-landlord");

        var (_, tenantToken) = await RegisterAndLoginAsync(UserRole.Tenant);
        var create = await _httpClient.PostAsJsonAsync("/api/caretakers/service-requests",
            new { caretakerId, propertyId, serviceType = "cleaning", description = "Deep clean" });
        var createBody = await create.Content.ReadAsStringAsync();
        Assert.True(create.StatusCode == HttpStatusCode.Created, $"Expected Created but got {create.StatusCode}: {createBody}");
        var requestId = JsonDocument.Parse(createBody).RootElement
            .GetProperty("data").GetProperty("serviceRequestId").GetString()!;

        // The requester cannot accept or complete their own request.
        var tenantCompletes = await _httpClient.PatchAsJsonAsync(
            $"/api/caretakers/service-requests/{requestId}/status", new { status = "Completed" });
        Assert.Equal(HttpStatusCode.BadRequest, tenantCompletes.StatusCode);

        // Caretaker accepts, works, completes.
        await LoginAsUserAsync(caretakerUserId);
        var accept = await _httpClient.PatchAsync($"/api/caretakers/service-requests/{requestId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var inProgress = await _httpClient.PatchAsJsonAsync(
            $"/api/caretakers/service-requests/{requestId}/status", new { status = "InProgress" });
        Assert.Equal(HttpStatusCode.OK, inProgress.StatusCode);

        var complete = await _httpClient.PatchAsJsonAsync(
            $"/api/caretakers/service-requests/{requestId}/status", new { status = "Completed" });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        // Requester reviews the completed job; the caretaker profile aggregates it.
        UseToken(tenantToken);
        var review = await _httpClient.PostAsJsonAsync(
            $"/api/caretakers/service-requests/{requestId}/review", new { rating = 5, comment = "Great job" });
        Assert.Equal(HttpStatusCode.Created, review.StatusCode);

        var profile = await _httpClient.GetAsync($"/api/caretakers/{caretakerId}");
        var profileData = JsonDocument.Parse(await profile.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.Equal(5.0, profileData.GetProperty("averageRating").GetDouble());
        Assert.Equal(1, profileData.GetProperty("reviewCount").GetInt32());

        // Creation + each transition + review notified the caretaker.
        Assert.True(CountNotifications(caretakerUserId) >= 2);
    }

    [Fact]
    public async Task ServiceRequest_CaretakerDeclines_AndRequesterCanCancelPending()
    {
        var (caretakerUserId, caretakerId) = await OnboardCaretakerAsync();
        var propertyId = await SeedPropertyAsync("some-landlord");

        var (_, tenantToken) = await RegisterAndLoginAsync(UserRole.Tenant);
        var first = await CreateServiceRequestAsync(caretakerId, propertyId);
        var second = await CreateServiceRequestAsync(caretakerId, propertyId);

        // Caretaker declines the first.
        await LoginAsUserAsync(caretakerUserId);
        var decline = await _httpClient.PatchAsync($"/api/caretakers/service-requests/{first}/decline", null);
        Assert.Equal(HttpStatusCode.OK, decline.StatusCode);

        // A declined request can't be accepted afterwards.
        var lateAccept = await _httpClient.PatchAsync($"/api/caretakers/service-requests/{first}/accept", null);
        Assert.Equal(HttpStatusCode.BadRequest, lateAccept.StatusCode);

        // Requester cancels the second while it is still pending.
        UseToken(tenantToken);
        var cancel = await _httpClient.PatchAsJsonAsync(
            $"/api/caretakers/service-requests/{second}/status", new { status = "Cancelled" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        // Reviews are rejected while a request isn't Completed.
        var review = await _httpClient.PostAsJsonAsync(
            $"/api/caretakers/service-requests/{second}/review", new { rating = 4, comment = "n/a" });
        Assert.Equal(HttpStatusCode.BadRequest, review.StatusCode);
    }

    private async Task<string> CreateServiceRequestAsync(string caretakerId, string propertyId)
    {
        var res = await _httpClient.PostAsJsonAsync("/api/caretakers/service-requests",
            new { caretakerId, propertyId, serviceType = "cleaning", description = "d" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement
            .GetProperty("data").GetProperty("serviceRequestId").GetString()!;
    }

    /// <summary>Re-authenticates the shared client as an already-registered user via password login.</summary>
    private async Task LoginAsUserAsync(string userId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = db.Users.Single(u => u.Id == userId).Email;

        var response = await _httpClient.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Password@123" });
        var token = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
        UseToken(token);
    }

    private void UseToken(string token) =>
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
}
