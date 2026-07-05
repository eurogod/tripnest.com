using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Claude-backed <see cref="IAiClient"/> using the official Anthropic SDK. When no API key is
/// configured (Ai:ApiKey), it degrades gracefully — <see cref="IsConfigured"/> is false and calls
/// return null — so the whole app runs locally without AI credentials, matching the house pattern
/// of the SMS/email/payment integrations. Provider errors are logged and surfaced as null, never
/// thrown: an AI hiccup must not 500 the request.
/// </summary>
public class ClaudeAiClient : IAiClient
{
    private readonly AnthropicClient? _client;
    private readonly string _model;
    private readonly ILogger<ClaudeAiClient> _logger;

    public ClaudeAiClient(IConfiguration configuration, ILogger<ClaudeAiClient> logger)
    {
        _logger = logger;
        _model = configuration["Ai:Model"] ?? "claude-opus-4-8";

        var apiKey = configuration["Ai:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
            _client = new AnthropicClient { ApiKey = apiKey };
    }

    public bool IsConfigured => _client is not null;

    public async Task<ListingCopySuggestion?> GenerateListingCopyAsync(
        Models.Property property, IReadOnlyList<AiImage> photos, CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            _logger.LogInformation("[AI not configured] skipping listing copy generation for property {PropertyId}", property.Id);
            return null;
        }

        try
        {
            var content = new List<ContentBlockParam>();
            foreach (var photo in photos)
            {
                content.Add(new ImageBlockParam
                {
                    Source = new Base64ImageSource
                    {
                        Data = Convert.ToBase64String(photo.Data),
                        MediaType = photo.MediaType,
                    },
                });
            }
            content.Add(new TextBlockParam { Text = ListingCopyPrompts.Facts(property) });

            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = _model,
                MaxTokens = 2048,
                Thinking = new ThinkingConfigAdaptive(),
                System = ListingCopyPrompts.System,
                // Structured output: the response is guaranteed to be valid JSON matching the
                // schema, so parsing below cannot fail on chatty preamble or markdown fences.
                OutputConfig = new OutputConfig
                {
                    Format = new JsonOutputFormat { Schema = SuggestionSchema },
                },
                Messages = [new() { Role = Role.User, Content = content }],
            }, cancellationToken: cancellationToken);

            var text = response.Content.Select(b => b.Value).OfType<TextBlock>().FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("AI returned no text for property {PropertyId} (stop reason: {StopReason})",
                    property.Id, response.StopReason);
                return null;
            }

            var suggestion = JsonSerializer.Deserialize<ListingCopySuggestion>(text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            _logger.LogInformation(
                "Listing copy generated for property {PropertyId} ({InputTokens} in / {OutputTokens} out)",
                property.Id, response.Usage.InputTokens, response.Usage.OutputTokens);
            return suggestion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI listing copy generation failed for property {PropertyId}", property.Id);
            return null;
        }
    }

    private static readonly Dictionary<string, JsonElement> SuggestionSchema = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(new
        {
            title = new { type = "string", description = "Listing title, under 60 characters" },
            description = new { type = "string", description = "2-3 short paragraphs" },
            highlights = new
            {
                type = "array",
                items = new { type = "string" },
                description = "3-5 short bullet phrases",
            },
        }),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "title", "description", "highlights" }),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
    };
}
