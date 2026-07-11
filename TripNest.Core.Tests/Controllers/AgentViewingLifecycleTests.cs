using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Agents;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for the viewing-request lifecycle: role-gated status transitions (a tenant can no
/// longer confirm/complete their own request), agent decline, tenant review of a completed
/// viewing → agent rating aggregation, and the serviceArea filter + paging on the agents list.
/// </summary>
public class AgentViewingLifecycleTests : TestBase
{
    private static UpsertAgentProfileRequest ValidProfile(string? area = "Accra") => new()
    {
        LicenseNumber = "GH-AG-2026-777",
        Bio = "Viewing lifecycle test agent",
        CommissionRate = 5m,
        YearsOfExperience = 3,
        ServiceArea = area
    };

    private async Task<(string UserId, string Token, string AgentId)> OnboardAgentAsync(string? area = "Accra")
    {
        var (userId, token) = await RegisterAndLoginAsync(UserRole.Agent);
        await MarkUserVerifiedAsync(userId);
        var put = await _httpClient.PutAsJsonAsync("/api/agents/me", ValidProfile(area));
        var body = await put.Content.ReadAsStringAsync();
        Assert.True(put.StatusCode == HttpStatusCode.OK, $"Expected OK but got {put.StatusCode}: {body}");
        var agentId = JsonDocument.Parse(body).RootElement
            .GetProperty("data").GetProperty("agentId").GetString()!;
        return (userId, token, agentId);
    }

    private async Task<string> SeedPropertyAsync()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = new Property
        {
            UserId = "some-landlord",
            Title = "Viewing Test Property",
            Description = "d",
            Location = "Accra",
            Latitude = 5.6,
            Longitude = -0.2,
            Bedrooms = 3,
            Bathrooms = 2,
            MonthlyRent = 2000m,
            DailyRate = 80m,
            PropertyType = "House"
        };
        db.Set<Property>().Add(property);
        await db.SaveChangesAsync();
        return property.Id;
    }

    [Fact]
    public async Task ViewingLifecycle_ConfirmComplete_ThenTenantReview_AggregatesOnAgent()
    {
        var (agentUserId, agentToken, agentId) = await OnboardAgentAsync();
        var propertyId = await SeedPropertyAsync();

        var (_, tenantToken) = await RegisterAndLoginAsync(UserRole.Tenant);
        var create = await _httpClient.PostAsJsonAsync($"/api/agents/{agentId}/viewing-requests",
            new { propertyId, scheduledAt = DateTime.UtcNow.AddDays(2), notes = "afternoon please" });
        var createBody = await create.Content.ReadAsStringAsync();
        Assert.True(create.StatusCode == HttpStatusCode.Created, $"Expected Created but got {create.StatusCode}: {createBody}");
        var requestId = JsonDocument.Parse(createBody).RootElement
            .GetProperty("data").GetProperty("viewingRequestId").GetString()!;

        // The agent was notified about the new request.
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(db.Set<Notification>().Any(n => n.UserId == agentUserId));
        }

        // The tenant cannot confirm their own request.
        var tenantConfirms = await _httpClient.PatchAsJsonAsync(
            $"/api/agents/viewing-requests/{requestId}/status", new { status = "Confirmed" });
        Assert.Equal(HttpStatusCode.BadRequest, tenantConfirms.StatusCode);

        // Agent confirms, then completes after the viewing happened.
        UseToken(agentToken);
        var confirm = await _httpClient.PatchAsJsonAsync(
            $"/api/agents/viewing-requests/{requestId}/status", new { status = "Confirmed" });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var completeRes = await _httpClient.PatchAsJsonAsync(
            $"/api/agents/viewing-requests/{requestId}/status", new { status = "Completed" });
        Assert.Equal(HttpStatusCode.OK, completeRes.StatusCode);

        // Tenant reviews the completed viewing; the agent profile aggregates it.
        UseToken(tenantToken);
        var review = await _httpClient.PostAsJsonAsync(
            $"/api/agents/viewing-requests/{requestId}/review", new { rating = 4, comment = "Helpful" });
        Assert.Equal(HttpStatusCode.Created, review.StatusCode);

        var profile = await _httpClient.GetAsync($"/api/agents/{agentId}");
        var profileData = JsonDocument.Parse(await profile.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.Equal(4.0, profileData.GetProperty("averageRating").GetDouble());
        Assert.Equal(1, profileData.GetProperty("reviewCount").GetInt32());
    }

    [Fact]
    public async Task CreateViewingRequest_InThePast_IsRejected()
    {
        var (_, _, agentId) = await OnboardAgentAsync();
        var propertyId = await SeedPropertyAsync();

        await RegisterAndLoginAsync(UserRole.Tenant);
        var response = await _httpClient.PostAsJsonAsync($"/api/agents/{agentId}/viewing-requests",
            new { propertyId, scheduledAt = DateTime.UtcNow.AddDays(-1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Agent_CanDeclinePendingRequest_TenantSeesDeclined()
    {
        var (_, agentToken, agentId) = await OnboardAgentAsync();
        var propertyId = await SeedPropertyAsync();

        var (_, tenantToken) = await RegisterAndLoginAsync(UserRole.Tenant);
        var create = await _httpClient.PostAsJsonAsync($"/api/agents/{agentId}/viewing-requests",
            new { propertyId, scheduledAt = DateTime.UtcNow.AddDays(1) });
        var requestId = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement
            .GetProperty("data").GetProperty("viewingRequestId").GetString()!;

        UseToken(agentToken);
        var decline = await _httpClient.PatchAsync($"/api/agents/viewing-requests/{requestId}/decline", null);
        Assert.Equal(HttpStatusCode.OK, decline.StatusCode);

        UseToken(tenantToken);
        var mine = await _httpClient.GetAsync("/api/agents/viewing-requests/mine");
        var mineData = JsonDocument.Parse(await mine.Content.ReadAsStringAsync()).RootElement.GetProperty("data").GetProperty("items");
        Assert.Equal("Declined", mineData[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task AgentsList_FiltersByServiceArea_AndPages()
    {
        await OnboardAgentAsync("Accra");
        await OnboardAgentAsync("Kumasi");

        var filtered = await _httpClient.GetAsync("/api/agents?serviceArea=kumasi");
        var filteredData = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.Equal(1, filteredData.GetProperty("items").GetArrayLength());
        Assert.Equal("Kumasi", filteredData.GetProperty("items")[0].GetProperty("serviceArea").GetString());

        var paged = await _httpClient.GetAsync("/api/agents?page=1&pageSize=1");
        var pagedData = JsonDocument.Parse(await paged.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.Equal(1, pagedData.GetProperty("items").GetArrayLength());
        Assert.Equal(2, pagedData.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, pagedData.GetProperty("totalPages").GetInt32());
    }

    private void UseToken(string token) =>
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
}
