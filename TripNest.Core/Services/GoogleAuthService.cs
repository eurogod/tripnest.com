using System.Text.Json;
using TripNest.Core.DTOs.Auth;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Validates a Google Sign-In ID token and extracts the user's identity. Uses Google's tokeninfo
/// endpoint and checks the token's audience against the configured client id, so a token minted for
/// a different app is rejected. Social sign-in is only enabled when <c>GoogleAuth:ClientId</c> is set.
/// </summary>
public class GoogleAuthService : IGoogleAuthService
{
    private readonly HttpClient _httpClient;
    private readonly string? _clientId;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(HttpClient httpClient, IConfiguration configuration, ILogger<GoogleAuthService> logger)
    {
        _httpClient = httpClient;
        _clientId = configuration["GoogleAuth:ClientId"];
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId);

    public async Task<GoogleUserInfo?> ValidateAsync(string idToken)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(idToken))
            return null;

        try
        {
            using var resp = await _httpClient.GetAsync(
                $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}");
            if (!resp.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            var aud = root.TryGetProperty("aud", out var a) ? a.GetString() : null;
            if (aud != _clientId)
            {
                _logger.LogWarning("Google token audience mismatch");
                return null;
            }

            var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
            if (string.IsNullOrWhiteSpace(email))
                return null;

            var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            var emailVerified = root.TryGetProperty("email_verified", out var v)
                && (v.ValueKind == JsonValueKind.True || v.GetString() == "true");

            return new GoogleUserInfo(email, string.IsNullOrWhiteSpace(name) ? email : name!, emailVerified);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google token validation failed");
            return null;
        }
    }
}
