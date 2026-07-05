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
/// Coverage for POST /api/auth/google. The real GoogleAuthService (which calls Google's tokeninfo
/// endpoint) is swapped for a controllable fake — mirroring how TestFixture swaps the SMS/email/
/// payment integrations — so every branch is exercised: unconfigured, invalid token, unverified
/// email (which must never provision or link an account), new-user provisioning, existing-account
/// sign-in, and inactive accounts.
/// </summary>
public class GoogleSignInTests : TestBase
{
    private readonly FakeGoogleAuthService _google = new();

    public GoogleSignInTests()
    {
        // Same fixture (and therefore the same in-memory database), with only the Google
        // validator replaced.
        _httpClient = _fixture.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGoogleAuthService>();
                services.AddSingleton<IGoogleAuthService>(_google);
            })).CreateClient();
    }

    private Task<HttpResponseMessage> SignInAsync() =>
        _httpClient.PostAsJsonAsync("/api/auth/google", new GoogleSignInRequest { IdToken = "test-id-token" });

    [Fact]
    public async Task GoogleSignIn_WhenNotConfigured_ReturnsBadRequest()
    {
        _google.IsConfigured = false;

        var response = await SignInAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GoogleSignIn_InvalidToken_ReturnsBadRequest()
    {
        _google.Identity = null;

        var response = await SignInAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GoogleSignIn_UnverifiedEmail_IsRejectedAndDoesNotProvision()
    {
        var email = $"unverified_{Guid.NewGuid():N}@example.com";
        _google.Identity = new GoogleUserInfo(email, "Unverified User", EmailVerified: false);

        var response = await SignInAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // No account may be created from an unverified email claim.
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.Email == email));
    }

    [Fact]
    public async Task GoogleSignIn_UnverifiedEmail_CannotSignInToExistingAccount()
    {
        // Account-takeover regression: a Google identity asserting an existing user's email
        // without email_verified must never be issued that user's tokens.
        var email = $"victim_{Guid.NewGuid():N}@example.com";
        await RegisterAndLoginAsync(UserRole.Tenant, email);
        ClearAuth();

        _google.Identity = new GoogleUserInfo(email, "Attacker", EmailVerified: false);

        var response = await SignInAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GoogleSignIn_NewUser_ProvisionsTenantAndIssuesWorkingTokens()
    {
        var email = $"new_{Guid.NewGuid():N}@example.com";
        _google.Identity = new GoogleUserInfo(email, "New Google User", EmailVerified: true);

        var response = await SignInAsync();
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected OK but got {response.StatusCode}: {body}");
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");
        Assert.Equal(email, data.GetProperty("email").GetString());
        Assert.Equal((int)UserRole.Tenant, data.GetProperty("role").GetInt32());
        Assert.True(data.GetProperty("emailVerified").GetBoolean());

        // The issued access token must work against a protected endpoint.
        var token = data.GetProperty("accessToken").GetString()!;
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var authed = await _httpClient.GetAsync("/api/escrow/mine");
        Assert.Equal(HttpStatusCode.OK, authed.StatusCode);
    }

    [Fact]
    public async Task GoogleSignIn_ExistingUser_SignsInToSameAccount()
    {
        var email = $"existing_{Guid.NewGuid():N}@example.com";
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant, email);
        ClearAuth();

        _google.Identity = new GoogleUserInfo(email, "Existing User", EmailVerified: true);

        var response = await SignInAsync();
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected OK but got {response.StatusCode}: {body}");
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");
        Assert.Equal(userId, data.GetProperty("userId").GetString());
    }

    [Fact]
    public async Task GoogleSignIn_InactiveAccount_IsRejected()
    {
        var email = $"inactive_{Guid.NewGuid():N}@example.com";
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant, email);
        ClearAuth();

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(userId);
            user!.IsActive = false;
            await db.SaveChangesAsync();
        }

        _google.Identity = new GoogleUserInfo(email, "Deactivated User", EmailVerified: true);

        var response = await SignInAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class FakeGoogleAuthService : IGoogleAuthService
    {
        public bool IsConfigured { get; set; } = true;

        /// <summary>Identity ValidateAsync returns; null simulates an invalid/rejected token.</summary>
        public GoogleUserInfo? Identity { get; set; }

        public Task<GoogleUserInfo?> ValidateAsync(string idToken) =>
            Task.FromResult(IsConfigured ? Identity : null);
    }
}
