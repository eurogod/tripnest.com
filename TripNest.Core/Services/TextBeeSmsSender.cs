using System.Net.Http.Headers;
using System.Net.Http.Json;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// SMS sender backed by TextBee (https://textbee.dev), which relays messages through a
/// registered Android device. Graceful fallback: if TextBeeSettings aren't configured
/// (missing API key or device id), it logs the message and returns false instead of
/// throwing, so notification side-effects never break the underlying business action.
/// </summary>
public class TextBeeSmsSender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly ILogger<TextBeeSmsSender> _logger;
    private readonly string? _apiKey;
    private readonly string? _deviceId;
    private readonly string _baseUrl;

    public TextBeeSmsSender(HttpClient http, IConfiguration configuration, ILogger<TextBeeSmsSender> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = configuration["TextBeeSettings:ApiKey"];
        _deviceId = configuration["TextBeeSettings:DeviceId"];
        _baseUrl = (configuration["TextBeeSettings:BaseUrl"] ?? "https://api.textbee.dev/api/v1").TrimEnd('/');
    }

    public async Task<bool> SendSmsAsync(string phoneNumber, string message)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_deviceId))
        {
            _logger.LogInformation("[SMS not configured] would send to {PhoneNumber}: {Message}", phoneNumber, message);
            return false;
        }

        try
        {
            var url = $"{_baseUrl}/gateway/devices/{_deviceId}/send-sms";
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(new
                {
                    recipients = new[] { phoneNumber },
                    message
                })
            };
            request.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _http.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SMS sent to {PhoneNumber} via TextBee", phoneNumber);
                return true;
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("TextBee returned {Status} sending to {PhoneNumber}: {Body}",
                (int)response.StatusCode, phoneNumber, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {PhoneNumber}", phoneNumber);
            return false;
        }
    }
}
