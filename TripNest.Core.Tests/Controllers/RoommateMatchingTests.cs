using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for roommate matching: profile lifecycle, reciprocity (no profile → no browsing),
/// compatibility ordering, hard-conflict exclusion, and filters.
/// </summary>
public class RoommateMatchingTests : TestBase
{
    private static object Profile(
        string location = "Accra, Ghana",
        decimal budget = 800m,
        string? university = "University of Ghana",
        bool smokes = false,
        bool okWithSmoker = false,
        bool nightOwl = false,
        int cleanliness = 3,
        bool isVisible = true) => new
    {
        bio = "Looking for a calm place near campus",
        university,
        preferredLocation = location,
        monthlyBudget = budget,
        smokes,
        okWithSmoker,
        hasPets = false,
        okWithPets = true,
        nightOwl,
        cleanlinessLevel = cleanliness,
        isVisible
    };

    [Fact]
    public async Task Profile_UpsertGetDelete_Works()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);

        // 404 until created.
        Assert.Equal(HttpStatusCode.NotFound, (await _httpClient.GetAsync("/api/roommates/me")).StatusCode);

        var saved = await DataOf(await _httpClient.PutAsJsonAsync("/api/roommates/me", Profile(budget: 650m)));
        Assert.Equal(650m, saved.GetProperty("monthlyBudget").GetDecimal());

        // Update in place (still one profile).
        var updated = await DataOf(await _httpClient.PutAsJsonAsync("/api/roommates/me", Profile(budget: 700m)));
        Assert.Equal(700m, updated.GetProperty("monthlyBudget").GetDecimal());

        Assert.Equal(HttpStatusCode.OK, (await _httpClient.DeleteAsync("/api/roommates/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _httpClient.GetAsync("/api/roommates/me")).StatusCode);
    }

    [Fact]
    public async Task Matches_RequireOwnVisibleProfile()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);

        // No profile yet → matching is mutual, so browsing is refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await _httpClient.GetAsync("/api/roommates/matches")).StatusCode);

        // A hidden profile doesn't unlock browsing either.
        await _httpClient.PutAsJsonAsync("/api/roommates/me", Profile(isVisible: false));
        Assert.Equal(HttpStatusCode.BadRequest, (await _httpClient.GetAsync("/api/roommates/matches")).StatusCode);
    }

    [Fact]
    public async Task Matches_RankByCompatibility_ExcludeHardConflictsAndHiddenProfiles()
    {
        // Great match: same city, same university, same budget, same habits.
        var (greatId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await _httpClient.PutAsJsonAsync("/api/roommates/me", Profile(budget: 800m));

        // Weaker match: different city and university, far budget, night owl.
        var (weakId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await _httpClient.PutAsJsonAsync("/api/roommates/me",
            Profile(location: "Kumasi, Ghana", budget: 200m, university: "KNUST", nightOwl: true, cleanliness: 1));

        // Hard conflict: smoker (the searcher is not OK with smokers) — must not appear at all.
        var (smokerId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await _httpClient.PutAsJsonAsync("/api/roommates/me", Profile(smokes: true, okWithSmoker: true));

        // Hidden profile — must not appear.
        var (hiddenId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await _httpClient.PutAsJsonAsync("/api/roommates/me", Profile(isVisible: false));

        // The searcher.
        await RegisterAndLoginAsync(UserRole.Tenant);
        await _httpClient.PutAsJsonAsync("/api/roommates/me", Profile(budget: 800m));

        var data = await DataOf(await _httpClient.GetAsync("/api/roommates/matches"));
        var items = data.GetProperty("items").EnumerateArray().ToList();
        var ids = items.Select(m => m.GetProperty("profile").GetProperty("userId").GetString()).ToList();

        Assert.Contains(greatId, ids);
        Assert.Contains(weakId, ids);
        Assert.DoesNotContain(smokerId, ids);
        Assert.DoesNotContain(hiddenId, ids);

        // Best match first, and the near-identical profile scores near the top of the scale.
        Assert.Equal(greatId, ids[0]);
        var topScore = items[0].GetProperty("score").GetInt32();
        var weakScore = items[ids.IndexOf(weakId)].GetProperty("score").GetInt32();
        Assert.True(topScore > weakScore, $"expected {topScore} > {weakScore}");
        Assert.True(topScore >= 90, $"near-identical profiles should score high, got {topScore}");
    }

    [Fact]
    public async Task Matches_FilterByLocationAndBudget()
    {
        var (accraId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await _httpClient.PutAsJsonAsync("/api/roommates/me", Profile(budget: 500m));

        var (kumasiId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await _httpClient.PutAsJsonAsync("/api/roommates/me", Profile(location: "Kumasi, Ghana", budget: 2000m));

        await RegisterAndLoginAsync(UserRole.Tenant);
        await _httpClient.PutAsJsonAsync("/api/roommates/me", Profile(budget: 600m));

        var filtered = await DataOf(await _httpClient.GetAsync("/api/roommates/matches?location=Accra&maxBudget=1000"));
        var ids = filtered.GetProperty("items").EnumerateArray()
            .Select(m => m.GetProperty("profile").GetProperty("userId").GetString()).ToList();

        Assert.Contains(accraId, ids);
        Assert.DoesNotContain(kumasiId, ids);
    }

    private static async Task<JsonElement> DataOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data");
    }
}
