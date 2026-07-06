using System.Text;
using System.Text.Json;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Google Gemini-backed <see cref="IAiClient"/> — the zero-cost option: Gemini's AI Studio free
/// tier needs no card and its daily quota comfortably covers listing-copy generation for a small
/// platform. Talks to the Generative Language REST API directly (same raw-HttpClient house
/// pattern as <see cref="PaystackPaymentGateway"/>). Selected via Ai:Provider=gemini; same
/// graceful-degradation contract as <see cref="ClaudeAiClient"/>: no Ai:Gemini:ApiKey → calls
/// return null and AI features switch off; provider errors are logged, never thrown.
/// </summary>
public class GeminiAiClient : IAiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiAiClient> _logger;
    private readonly string? _apiKey;
    private readonly string _model;

    public GeminiAiClient(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiAiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Ai:Gemini:ApiKey"];
        _model = configuration["Ai:Gemini:Model"] ?? "gemini-2.5-flash";

        _httpClient.BaseAddress ??= new Uri("https://generativelanguage.googleapis.com/");
        // Key travels as a header, not a query parameter, so it never lands in URL logs.
        if (!string.IsNullOrWhiteSpace(_apiKey))
            _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", _apiKey);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<ListingCopySuggestion?> GenerateListingCopyAsync(
        Models.Property property, IReadOnlyList<AiImage> photos, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogInformation("[Gemini not configured] skipping listing copy generation for property {PropertyId}", property.Id);
            return null;
        }

        try
        {
            var parts = new List<object>();
            foreach (var photo in photos)
            {
                parts.Add(new
                {
                    inlineData = new
                    {
                        mimeType = photo.MediaType,
                        data = Convert.ToBase64String(photo.Data),
                    },
                });
            }
            parts.Add(new { text = ListingCopyPrompts.Facts(property) });

            var payload = new
            {
                systemInstruction = new { parts = new[] { new { text = ListingCopyPrompts.System } } },
                contents = new[] { new { role = "user", parts } },
                // Structured output: Gemini constrains the response to this schema, so the reply
                // is guaranteed-parseable JSON — same safety as the Claude implementation.
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    responseSchema = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            title = new { type = "STRING" },
                            description = new { type = "STRING" },
                            highlights = new { type = "ARRAY", items = new { type = "STRING" } },
                        },
                        required = new[] { "title", "description", "highlights" },
                    },
                },
            };

            using var response = await _httpClient.PostAsync(
                $"v1beta/models/{Uri.EscapeDataString(_model)}:generateContent",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini generateContent failed ({Status}) for property {PropertyId}: {Body}",
                    response.StatusCode, property.Id, json);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            // Safety filters can return an empty candidate list — treat as a soft failure.
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                _logger.LogWarning("Gemini returned no candidates for property {PropertyId}", property.Id);
                return null;
            }

            var text = string.Concat(candidates[0]
                .GetProperty("content").GetProperty("parts").EnumerateArray()
                .Where(p => p.TryGetProperty("text", out _))
                .Select(p => p.GetProperty("text").GetString()));
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Gemini returned empty text for property {PropertyId}", property.Id);
                return null;
            }

            var suggestion = JsonSerializer.Deserialize<ListingCopySuggestion>(text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            _logger.LogInformation("Listing copy generated for property {PropertyId} via Gemini ({Model})",
                property.Id, _model);
            return suggestion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini listing copy generation failed for property {PropertyId}", property.Id);
            return null;
        }
    }

    public async Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogInformation("[Gemini not configured] skipping completion");
            return null;
        }

        try
        {
            var payload = new
            {
                systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
                // Callers prompt for JSON; forcing the mime type keeps the reply fence-free.
                generationConfig = new { responseMimeType = "application/json" },
            };

            using var response = await _httpClient.PostAsync(
                $"v1beta/models/{Uri.EscapeDataString(_model)}:generateContent",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini completion failed ({Status}): {Body}", response.StatusCode, json);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                return null;

            return string.Concat(candidates[0]
                .GetProperty("content").GetProperty("parts").EnumerateArray()
                .Where(p => p.TryGetProperty("text", out _))
                .Select(p => p.GetProperty("text").GetString()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini completion failed");
            return null;
        }
    }
}
