using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TripNest.Core.DTOs.Auth;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Validates a Facebook Login access token and extracts the user's identity via the Graph API.
/// Enabled only when <c>FacebookAuth:AppId</c> and <c>FacebookAuth:AppSecret</c> are configured
/// (the secret belongs in user-secrets/env, never appsettings.json).
/// </summary>
public class FacebookAuthService : IFacebookAuthService
{
    private readonly HttpClient _httpClient;
    private readonly string? _appId;
    private readonly string? _appSecret;
    private readonly ILogger<FacebookAuthService> _logger;

    public FacebookAuthService(HttpClient httpClient, IConfiguration configuration, ILogger<FacebookAuthService> logger)
    {
        _httpClient = httpClient;
        _appId = configuration["FacebookAuth:AppId"];
        _appSecret = configuration["FacebookAuth:AppSecret"];
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_appId) && !string.IsNullOrWhiteSpace(_appSecret);

    public async Task<FacebookUserInfo?> ValidateAsync(string accessToken)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(accessToken))
            return null;

        try
        {
            // appsecret_proof (HMAC of the token with our app secret) both authenticates this server
            // to Facebook and binds the token to our app: Graph checks the proof against the secret
            // of the app the token was minted for, so a token from another app is rejected here —
            // no separate debug_token round-trip needed. The token itself travels in the
            // Authorization header, not the query string, so it can't leak into request logs.
            var proof = Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(_appSecret!), Encoding.UTF8.GetBytes(accessToken))).ToLowerInvariant();

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://graph.facebook.com/v19.0/me?fields=id,name,email&appsecret_proof={proof}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var resp = await _httpClient.SendAsync(request);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Facebook token validation rejected with {Status}", resp.StatusCode);
                return null;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Phone-registered Facebook accounts (or a denied email permission) have no email.
            var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;

            return new FacebookUserInfo(string.IsNullOrWhiteSpace(email) ? null : email, name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Facebook token validation failed");
            return null;
        }
    }
}
