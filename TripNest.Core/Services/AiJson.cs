using System.Text.Json;

namespace TripNest.Core.Services;

/// <summary>
/// Tolerant JSON extraction for AI completions: models occasionally wrap JSON in markdown
/// fences or a stray sentence, so find the outermost object before deserialising. A parse
/// failure returns null — callers treat it exactly like a provider failure.
/// </summary>
internal static class AiJson
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static T? TryParse<T>(string? text) where T : class
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(text[start..(end + 1)], Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
