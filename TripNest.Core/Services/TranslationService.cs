using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

public class TranslationService : ITranslationService
{
    // Translations of the same text never change — cache them for a long time. The key is a hash
    // of (text + language), so the same notification template with the same interpolated values
    // (e.g. "Rent of GHS 900 received") is translated once and reused across every user who sees it.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(30);

    private readonly IAiClient _aiClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TranslationService> _logger;

    private sealed class TranslationJson { public string? Title { get; set; } public string? Message { get; set; } }

    public TranslationService(IAiClient aiClient, IMemoryCache cache, ILogger<TranslationService> logger)
    {
        _aiClient = aiClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<(string Title, string Message)> TranslateNotificationAsync(
        string title, string message, Language language, CancellationToken cancellationToken = default)
    {
        // English is the source language, and there's nothing to do (or no way to do it) without
        // a provider — return the original text untouched. Never throws: a translation miss must
        // never break the notifications list.
        if (language == Language.English || !_aiClient.IsConfigured)
            return (title, message);

        var cacheKey = $"tr:{(int)language}:{Hash(title + "" + message)}";
        if (_cache.TryGetValue<(string, string)>(cacheKey, out var hit))
            return hit;

        try
        {
            var raw = await _aiClient.CompleteAsync(
                $"You translate short app notifications into {language.ToPromptName()}. Keep it natural and " +
                "concise, preserve numbers/currency/dates exactly, and do not add anything. " +
                "Reply ONLY with JSON: {\"title\": string, \"message\": string}.",
                $"Title: {title}\nMessage: {message}",
                cancellationToken);

            var parsed = AiJson.TryParse<TranslationJson>(raw);
            if (parsed?.Title is null || parsed.Message is null)
                return (title, message);

            var result = (parsed.Title, parsed.Message);
            _cache.Set(cacheKey, result, CacheTtl);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification translation to {Language} failed — showing original text", language);
            return (title, message);
        }
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
