using TripNest.Core.DTOs.Properties;
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
        Property property, IReadOnlyList<AiImage> photos, CancellationToken cancellationToken = default);
}
