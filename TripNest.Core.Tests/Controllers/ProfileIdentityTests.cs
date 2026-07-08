using System.Net;
using System.Text.Json;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Profile-edit integrity: FullName is bound to the verified Ghana Card identity (editable before
/// verification, locked after), the preferred language round-trips through the profile GET, and an
/// out-of-range language value is rejected rather than silently stored.
/// </summary>
public class ProfileIdentityTests : TestBase
{
    private async Task<JsonElement> GetProfileAsync()
    {
        var res = await _httpClient.GetAsync("/api/profile/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
    }

    [Fact]
    public async Task FullName_EditableBeforeVerification()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);

        var res = await _httpClient.PutAsJsonAsync("/api/profile/me", new { fullName = "Kwame Mensah" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Kwame Mensah", (await GetProfileAsync()).GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task FullName_ChangeAfterVerification_IsRejected()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await MarkUserVerifiedAsync(userId);

        var res = await _httpClient.PutAsJsonAsync("/api/profile/me", new { fullName = "John Smith" });
        var body = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("locked", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifiedUser_CanStillEditOtherFields_WithoutResendingName()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await MarkUserVerifiedAsync(userId);

        // A verified user editing bio/language must not be blocked by the name lock (name omitted,
        // or re-sent unchanged, is fine — only an actual change is refused).
        var res = await _httpClient.PutAsJsonAsync("/api/profile/me",
            new { bio = "Frequent traveller", preferredLanguage = (int)Language.Twi });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task PreferredLanguage_RoundTripsThroughProfileGet()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);

        var res = await _httpClient.PutAsJsonAsync("/api/profile/me", new { preferredLanguage = (int)Language.Ga });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var data = await GetProfileAsync();
        Assert.Equal((int)Language.Ga, data.GetProperty("preferredLanguage").GetInt32());
    }

    [Fact]
    public async Task PreferredLanguage_OutOfRangeValue_IsRejected()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);

        var res = await _httpClient.PutAsJsonAsync("/api/profile/me", new { preferredLanguage = 440 });
        var body = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("language", body, StringComparison.OrdinalIgnoreCase);
    }
}
