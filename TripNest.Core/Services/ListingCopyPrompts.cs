using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

/// <summary>
/// Provider-agnostic prompting for listing copy generation, shared by every <c>IAiClient</c>
/// implementation so switching providers (Claude/Gemini) changes the model, not the brief.
/// </summary>
internal static class ListingCopyPrompts
{
    public static string System(Language language) =>
        "You write listing copy for TripNest, an accommodation-booking platform in Ghana. " +
        "From the property facts (and photos when provided) draft copy that is warm, specific and honest. " +
        "Never invent amenities, views or features that are not in the facts or clearly visible in the photos. " +
        "Mention the location naturally. " +
        "The title must be under 60 characters and must not start with generic filler like 'Cozy' or 'Stunning'. " +
        "The description is 2-3 short paragraphs. Highlights are 3-5 short bullet phrases, each under 8 words. " +
        $"Write ALL copy (title, description, highlights) in {language.ToPromptName()}.";

    public static string Facts(Property p)
    {
        var rate = p.DailyRate is not null
            ? $"GH₵{p.DailyRate:0.00} per night"
            : $"GH₵{p.MonthlyRent:0.00} per month";
        return $"""
            Draft listing copy for this property:
            - Type: {p.PropertyType} ({p.StayType})
            - Location: {p.Location}
            - Bedrooms: {p.Bedrooms}, Bathrooms: {p.Bathrooms}
            - Rate: {rate}
            - Amenities: {(string.IsNullOrWhiteSpace(p.Amenities) ? "not specified" : p.Amenities)}
            - Host's current title: {p.Title}
            - Host's current description: {p.Description}
            """;
    }
}
