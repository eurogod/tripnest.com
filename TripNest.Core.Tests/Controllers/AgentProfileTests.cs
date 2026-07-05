using System.Net;
using System.Text.Json;
using TripNest.Core.DTOs.Agents;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for the self-service agent directory profile (GET/PUT /api/agents/me) — the missing
/// piece that made GET /api/agents return empty even when Agent-role users existed: the public
/// list reads Agent profile rows, and this endpoint is how those rows get created.
/// </summary>
public class AgentProfileTests : TestBase
{
    private static UpsertAgentProfileRequest ValidProfile() => new()
    {
        LicenseNumber = "GH-AG-2026-001",
        Bio = "Experienced Accra rental agent",
        CommissionRate = 5m,
        YearsOfExperience = 4
    };

    [Fact]
    public async Task UpsertProfile_ThenAgentAppearsInPublicList()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Agent);
        await MarkUserVerifiedAsync(userId);

        var put = await _httpClient.PutAsJsonAsync("/api/agents/me", ValidProfile());
        var putBody = await put.Content.ReadAsStringAsync();
        Assert.True(put.StatusCode == HttpStatusCode.OK, $"Expected OK but got {put.StatusCode}: {putBody}");

        var list = await _httpClient.GetAsync("/api/agents");
        var data = JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement.GetProperty("data");

        Assert.Equal(1, data.GetArrayLength());
        Assert.Equal(userId, data[0].GetProperty("userId").GetString());
        Assert.Equal("GH-AG-2026-001", data[0].GetProperty("licenseNumber").GetString());
    }

    [Fact]
    public async Task GetMyProfile_BeforeCreating_ReturnsNotFound()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Agent);
        await MarkUserVerifiedAsync(userId);

        var response = await _httpClient.GetAsync("/api/agents/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpsertProfile_UnverifiedAgent_IsForbidden()
    {
        await RegisterAndLoginAsync(UserRole.Agent);

        var response = await _httpClient.PutAsJsonAsync("/api/agents/me", ValidProfile());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpsertProfile_NonAgentRole_IsForbidden()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await MarkUserVerifiedAsync(userId);

        var response = await _httpClient.PutAsJsonAsync("/api/agents/me", ValidProfile());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpsertProfile_SecondPut_UpdatesInsteadOfDuplicating()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Agent);
        await MarkUserVerifiedAsync(userId);

        await _httpClient.PutAsJsonAsync("/api/agents/me", ValidProfile());

        var updated = ValidProfile();
        updated.Bio = "Now covering Kumasi too";
        var second = await _httpClient.PutAsJsonAsync("/api/agents/me", updated);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var me = await _httpClient.GetAsync("/api/agents/me");
        var data = JsonDocument.Parse(await me.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.Equal("Now covering Kumasi too", data.GetProperty("bio").GetString());

        var list = await _httpClient.GetAsync("/api/agents");
        var listData = JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.Equal(1, listData.GetArrayLength());
    }

    [Fact]
    public async Task UpsertProfile_InvalidCommission_IsRejected()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Agent);
        await MarkUserVerifiedAsync(userId);

        var request = ValidProfile();
        request.CommissionRate = 150m;
        var response = await _httpClient.PutAsJsonAsync("/api/agents/me", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
