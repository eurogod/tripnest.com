using System.Net;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// A username is a public handle identifying one account: another user must not be able to take
/// it — not even with different casing — while the owner can freely re-save or change their own.
/// </summary>
public class ProfileUsernameTests : TestBase
{
    private Task<HttpResponseMessage> SetUsernameAsync(string username) =>
        _httpClient.PutAsJsonAsync("/api/profile/me", new { username });

    [Fact]
    public async Task Username_TakenByAnotherUser_IsRejectedCaseInsensitively()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        var first = await SetUsernameAsync("kwame_accra");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        await RegisterAndLoginAsync(UserRole.Tenant);
        var duplicate = await SetUsernameAsync("Kwame_Accra");
        var body = await duplicate.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("already taken", body);
    }

    [Fact]
    public async Task Username_ReSavingOwnHandle_IsAllowed()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        await SetUsernameAsync("ama_kumasi");

        var again = await SetUsernameAsync("ama_kumasi");

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }
}
