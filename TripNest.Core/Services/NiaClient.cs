using System.Text;
using System.Text.Json;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Client for the TripNest.Id authority service, which acts as the Ghana Card registry.
/// Calls POST /api/verification/verify with { cardId } and reads the ApiResponse envelope.
/// </summary>
public class NiaClient : INiaClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NiaClient> _logger;

    public NiaClient(HttpClient httpClient, IConfiguration configuration, ILogger<NiaClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<NiaVerificationResult> VerifyGhanaCardAsync(string cardId)
    {
        var baseUrl = (_configuration["Services:TripNestId"] ?? string.Empty).TrimEnd('/');

        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { cardId }),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{baseUrl}/api/verification/verify", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TripNest.Id returned status {StatusCode} for card {CardId}", response.StatusCode, cardId);
                return new NiaVerificationResult { IsValid = false, Status = "ServiceError" };
            }

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            // The authority wraps the payload in an ApiResponse envelope: { success, message, data: { ... } }
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning("TripNest.Id response for card {CardId} had no data payload", cardId);
                return new NiaVerificationResult { IsValid = false, Status = "ServiceError" };
            }

            var isValid = data.TryGetProperty("isValid", out var v) && v.GetBoolean();
            var status = data.TryGetProperty("status", out var s) ? s.GetString() ?? string.Empty : string.Empty;
            var fullName = data.TryGetProperty("fullName", out var fn) ? fn.GetString() : null;

            DateOnly? dob = null;
            if (data.TryGetProperty("dateOfBirth", out var d) && d.ValueKind == JsonValueKind.String
                && DateTime.TryParse(d.GetString(), out var parsed))
            {
                dob = DateOnly.FromDateTime(parsed);
            }

            // photoUrl comes back as a relative path (e.g. /photos/x.jpg) served by TripNest.Id —
            // make it absolute so the face-match sidecar can fetch it.
            string? photoUrl = null;
            if (data.TryGetProperty("photoUrl", out var p) && !string.IsNullOrWhiteSpace(p.GetString()))
            {
                var raw = p.GetString()!;
                // Only treat it as already-absolute when it's a real http(s) URL. We can't use
                // Uri.TryCreate(UriKind.Absolute) here: on Unix it parses a leading-slash path like
                // "/photos/x.jpg" as an absolute file:// URI, so the relative NIA path would wrongly
                // pass the check and reach the sidecar without a scheme/host.
                var isHttpAbsolute = Uri.TryCreate(raw, UriKind.Absolute, out var parsedPhotoUri)
                    && (parsedPhotoUri.Scheme == Uri.UriSchemeHttp || parsedPhotoUri.Scheme == Uri.UriSchemeHttps);
                photoUrl = isHttpAbsolute ? raw : $"{baseUrl}/{raw.TrimStart('/')}";
            }

            return new NiaVerificationResult
            {
                IsValid = isValid,
                PhotoUrl = photoUrl,
                FullName = fullName,
                DateOfBirth = dob,
                Status = status
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Ghana card {CardId} with TripNest.Id", cardId);
            return new NiaVerificationResult { IsValid = false, Status = "ServiceError" };
        }
    }
}
