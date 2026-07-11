namespace TripNest.Core.Models;

/// <summary>
/// An opt-in "looking for roommates" profile. Matching is reciprocal — only users with a visible
/// profile can browse others — and scored on budget, location, and living-habit compatibility.
/// A match leads into the existing flows: chat to talk, then a group booking (split billing) to
/// actually move in together.
/// </summary>
public class RoommateProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string UserId { get; set; }
    public User? User { get; set; }

    public string? Bio { get; set; }
    /// <summary>University or workplace — the student-housing anchor for matching.</summary>
    public string? University { get; set; }
    public required string PreferredLocation { get; set; }
    public decimal MonthlyBudget { get; set; }
    public DateTime? MoveInDate { get; set; }

    // Living habits. "Smokes/HasPets" describe the person; "OkWith…" describe their tolerance —
    // a smoker and a no-smoking profile are a hard conflict, not just a low score.
    public bool Smokes { get; set; }
    public bool OkWithSmoker { get; set; }
    public bool HasPets { get; set; }
    public bool OkWithPets { get; set; }
    public bool NightOwl { get; set; }
    /// <summary>1 (relaxed) to 5 (spotless).</summary>
    public int CleanlinessLevel { get; set; } = 3;

    /// <summary>Hidden profiles neither appear in matches nor may browse them.</summary>
    public bool IsVisible { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
