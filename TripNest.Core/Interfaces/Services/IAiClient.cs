using TripNest.Core.DTOs.Properties;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Services;

/// <summary>An image handed to the AI model (already loaded from storage and size-checked).</summary>
public record AiImage(byte[] Data, string MediaType);

/// <summary>
/// Abstraction over the AI model provider (Claude). Follows the same graceful-degradation
/// contract as <see cref="ISmsSender"/> / <see cref="IPaymentGateway"/>: when no API key is
/// configured, <see cref="IsConfigured"/> is false and calls return null instead of throwing,
/// so AI features simply switch off rather than breaking the app. Tests swap in a stub.
/// AI output is always advisory — it must never drive money movement or verification outcomes.
/// </summary>
public interface IAiClient
{
    bool IsConfigured { get; }

    /// <summary>
    /// Drafts listing copy (title, description, highlights) from the property's structured facts
    /// and up to a few listing photos. Returns null when unconfigured or when the provider call
    /// fails — the caller decides how to surface that.
    /// </summary>
    Task<ListingCopySuggestion?> GenerateListingCopyAsync(
        Property property, IReadOnlyList<AiImage> photos, Language language, CancellationToken cancellationToken = default);

    /// <summary>
    /// General text completion for the assistant / chat features. The system prompt should
    /// instruct the model to reply with JSON; callers parse via <c>AiJson.TryParse</c> and treat
    /// unparseable output like a provider failure. Returns null when unconfigured or on error.
    /// </summary>
    Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Multimodal completion: text prompt plus images (photo screening, claim-evidence
    /// description, walkthrough consistency checks). Same JSON-reply + null-on-failure contract
    /// as <see cref="CompleteAsync"/>.
    /// </summary>
    Task<string?> CompleteWithImagesAsync(string systemPrompt, string userPrompt,
        IReadOnlyList<AiImage> images, CancellationToken cancellationToken = default);
}
