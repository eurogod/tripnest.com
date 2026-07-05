using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Auth;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for POST /api/auth/facebook. The real FacebookAuthService (which calls the Graph API)
/// is swapped for a controllable fake, mirroring the Google sign-in tests: unconfigured, invalid
/// token, email-less Facebook account (must never provision), new-user provisioning, and
/// existing-account sign-in.
/// </summary>
public class FacebookSignInTests : TestBase
{
    private readonly FakeFacebookAuthService _facebook = new();

    public FacebookSignInTests()
    {
        _httpClient = _fixture.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFacebookAuthService>();
                services.AddSingleton<IFacebookAuthService>(_facebook);
            })).CreateClient();
    }

    private Task<HttpResponseMessage> SignInAsync() =>
        _httpClient.PostAsJsonAsync("/api/auth/facebook", new FacebookSignInRequest { AccessToken = "test-fb-token" });

    [Fact]
    public async Task FacebookSignIn_WhenNotConfigured_ReturnsBadRequest()
    {
        _facebook.IsConfigured = false;

        var response = await SignInAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FacebookSignIn_InvalidToken_ReturnsBadRequest()
    {
        _facebook.Identity = null;

        var response = await SignInAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FacebookSignIn_AccountWithoutEmail_IsRejectedAndDoesNotProvision()
    {
        _facebook.Identity = new FacebookUserInfo(Email: null, "Phone Registered User");

        var response = await SignInAsync();
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("no email address", body);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.FullName == "Phone Registered User"));
    }

    [Fact]
    public async Task FacebookSignIn_NewUser_ProvisionsTenantAndIssuesWorkingTokens()
    {
        var email = $"fbnew_{Guid.NewGuid():N}@example.com";
        _facebook.Identity = new FacebookUserInfo(email, "New Facebook User");

        var response = await SignInAsync();
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected OK but got {response.StatusCode}: {body}");
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");
        Assert.Equal(email, data.GetProperty("email").GetString());
        Assert.Equal((int)UserRole.Tenant, data.GetProperty("role").GetInt32());
        Assert.True(data.GetProperty("emailVerified").GetBoolean());

        var token = data.GetProperty("accessToken").GetString()!;
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var authed = await _httpClient.GetAsync("/api/escrow/mine");
        Assert.Equal(HttpStatusCode.OK, authed.StatusCode);
    }

    [Fact]
    public async Task FacebookSignIn_ExistingUser_SignsInToSameAccount()
    {
        var email = $"fbexisting_{Guid.NewGuid():N}@example.com";
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant, email);
        ClearAuth();

        _facebook.Identity = new FacebookUserInfo(email, "Existing User");

        var response = await SignInAsync();
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected OK but got {response.StatusCode}: {body}");
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");
        Assert.Equal(userId, data.GetProperty("userId").GetString());
    }

    private sealed class FakeFacebookAuthService : IFacebookAuthService
    {
        public bool IsConfigured { get; set; } = true;

        /// <summary>Identity ValidateAsync returns; null simulates an invalid/rejected token.</summary>
        public FacebookUserInfo? Identity { get; set; }

        public Task<FacebookUserInfo?> ValidateAsync(string accessToken) =>
            Task.FromResult(IsConfigured ? Identity : null);
    }
}
