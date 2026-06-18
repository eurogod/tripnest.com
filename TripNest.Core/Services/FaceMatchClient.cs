using System.Text;
using System.Text.Json;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

public class FaceMatchClient : IFaceMatchClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FaceMatchClient> _logger;

    public FaceMatchClient(HttpClient httpClient, IConfiguration configuration, ILogger<FaceMatchClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(double Score, string? FailureReason)> CompareFacesAsync(string selfiePhotoPath, string niaPhotoUrl)
    {
        try
        {
            var sidecarUrl = _configuration["Services:FaceMatchSidecar"] ?? "http://localhost:5001";
            var compareFacesUrl = $"{sidecarUrl}/compare-faces";

            // Read selfie from disk and encode to base64
            if (!File.Exists(selfiePhotoPath))
            {
                _logger.LogWarning("Selfie photo not found at path: {Path}", selfiePhotoPath);
                return (0.0, "Selfie photo file not found");
            }

            var selfieBytes = await File.ReadAllBytesAsync(selfiePhotoPath);
            var selfieBase64 = Convert.ToBase64String(selfieBytes);

            var requestBody = new
            {
                photo1_url = niaPhotoUrl,
                photo2_base64 = selfieBase64
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(compareFacesUrl, jsonContent);

            if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity) // 422
            {
                _logger.LogWarning("No face detected in one of the photos");
                return (0.0, "No face detected in uploaded photo. Please ensure the photo is clear and shows your face.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.FailedDependency) // 424
            {
                _logger.LogWarning("Could not retrieve NIA reference photo");
                return (0.0, "Could not retrieve reference photo from NIA service. Please try again.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Sidecar returned error: {StatusCode}", response.StatusCode);
                return (0.0, "Face matching service temporarily unavailable. Please try again.");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("similarity_score", out var scoreElement) && scoreElement.TryGetDouble(out var score))
            {
                _logger.LogInformation("Face match comparison completed with score {Score}", score);
                return (score, null);
            }

            _logger.LogError("Unexpected response format from sidecar: {Response}", responseContent);
            return (0.0, "Face matching service returned unexpected response format");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during face matching with sidecar");
            return (0.0, "Face matching service is unavailable. Please try again later.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during face matching");
            return (0.0, "An error occurred during face matching. Please try again.");
        }
    }
}
