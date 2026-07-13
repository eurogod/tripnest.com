namespace TripNest.Core.DTOs.Ai;

/// <summary>"What guests say" — themes distilled from a listing's reviews.</summary>
public class ReviewSummaryResponse
{
    public required string Summary { get; set; }
    public List<string> Positives { get; set; } = new();
    public List<string> Negatives { get; set; } = new();
    public int ReviewCount { get; set; }
}

/// <summary>Neutral admin brief for a damage claim or escrow dispute. Advisory only.</summary>
public class AdminBriefResponse
{
    public required string Brief { get; set; }
    public List<string> KeyPoints { get; set; } = new();
    public List<string> Inconsistencies { get; set; } = new();
    /// <summary>Always present so nobody forgets: the human decides, not the model.</summary>
    public string Disclaimer { get; set; } = "AI-generated summary for reading speed only — verify against the raw claim before deciding.";
}

/// <summary>Natural-language search: what the model understood plus the matching listings.</summary>
public class NaturalSearchResponse
{
    public required Search.PropertySearchCriteria Criteria { get; set; }
    public required List<Properties.PropertyResponse> Results { get; set; }
    public int TotalCount { get; set; }
}

/// <summary>Plain-language agreement explanation in the reader's preferred language.</summary>
public class AgreementSummaryResponse
{
    public required string Summary { get; set; }
    public List<string> KeyTerms { get; set; } = new();
    public List<string> YourObligations { get; set; } = new();
    public string Disclaimer { get; set; } = "This is a simplified explanation — the signed terms are the binding text.";
}

/// <summary>Listing quality report: deterministic checks + AI coaching (+ photo notes when vision is available).</summary>
public class ListingQualityResponse
{
    /// <summary>0–100, computed from the checks in code (not by the model).</summary>
    public int Score { get; set; }
    public List<QualityCheck> Checks { get; set; } = new();
    public List<string> AiSuggestions { get; set; } = new();
    public List<string> PhotoNotes { get; set; } = new();
}

public class QualityCheck
{
    public required string Name { get; set; }
    public bool Passed { get; set; }
    public required string Detail { get; set; }
}

/// <summary>Why two roommate profiles scored the way they did — sentences, not just numbers.</summary>
public class RoommateExplanationResponse
{
    public required string Explanation { get; set; }
    public List<string> SharedTraits { get; set; } = new();
    public List<string> Considerations { get; set; } = new();
}

/// <summary>Vision-assisted consistency check shown to walkthrough reviewers. Advisory only.</summary>
public class WalkthroughAiCheckResponse
{
    public bool PhotosConsistentWithListing { get; set; }
    public List<string> Observations { get; set; } = new();
    public List<string> RedFlags { get; set; } = new();
    public string Disclaimer { get; set; } = "Photo-based assist for the human reviewer — approval remains a human decision.";
}
