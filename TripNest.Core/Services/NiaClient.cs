using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

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

    public async Task<(bool IsValid, string PhotoUrl)> VerifyGhanaCardAsync(string idNumber, string firstName, string lastName, DateOnly dateOfBirth)
    {
        try
        {
            var niaServiceUrl = _configuration["Services:TripNestId"];
            var endpoint = $"{niaServiceUrl}/api/verification/verify";

            var request = new
            {
                idNumber,
                firstName,
                lastName,
                dateOfBirth = dateOfBirth.ToString("yyyy-MM-dd")
            };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonDoc = System.Text.Json.JsonDocument.Parse(responseContent);
                var root = jsonDoc.RootElement;

                var isValid = root.GetProperty("isValid").GetBoolean();
                var photoUrl = root.GetProperty("photoUrl").GetString() ?? string.Empty;

                return (isValid, photoUrl);
            }

            _logger.LogWarning("NIA service returned status {StatusCode} for card {CardNumber}", response.StatusCode, idNumber);
            return (false, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Ghana card with NIA service");
            return (false, string.Empty);
        }
    }
}
