using TripNest.Core.Enums;

namespace TripNest.Core.Interfaces.Services;

/// <summary>
/// On-demand, cached translation of short user-facing strings (notification title + body) into
/// the reader's language. Deliberately a READ-path concern: nothing on the write path (background
/// workers, payment webhooks, NotificationService.CreateAsync) ever calls it, so notifications are
/// always recorded instantly in English and only rendered in another language when a user actually
/// fetches them. English is a no-op; results are cached so repeated reads — and identical
/// templated strings across users — cost nothing. Falls back to the original text when the AI
/// provider is unconfigured or fails.
/// </summary>
public interface ITranslationService
{
    Task<(string Title, string Message)> TranslateNotificationAsync(
        string title, string message, Language language, CancellationToken cancellationToken = default);
}
