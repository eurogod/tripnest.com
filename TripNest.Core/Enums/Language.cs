namespace TripNest.Core.Enums;

/// <summary>
/// A user's preferred language for AI-generated, user-facing text (assistant replies, chat
/// suggestions, listing copy). English is the default. Twi and Ga serve the Ghanaian market;
/// French covers the wider region.
/// </summary>
public enum Language
{
    English,
    Twi,
    Ga,
    French
}

public static class LanguageExtensions
{
    /// <summary>The name to use when instructing an AI model which language to write in.</summary>
    public static string ToPromptName(this Language language) => language switch
    {
        Language.Twi => "Twi (Akan, as spoken in Ghana)",
        Language.Ga => "Ga (as spoken in Ghana)",
        Language.French => "French",
        _ => "English",
    };
}
