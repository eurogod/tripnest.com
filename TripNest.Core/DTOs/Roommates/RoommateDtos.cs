using System.ComponentModel.DataAnnotations;

namespace TripNest.Core.DTOs.Roommates;

public class UpsertRoommateProfileRequest
{
    [StringLength(500)]
    public string? Bio { get; set; }
    [StringLength(150)]
    public string? University { get; set; }
    [Required, StringLength(200)]
    public required string PreferredLocation { get; set; }
    [Range(1, 1_000_000)]
    public decimal MonthlyBudget { get; set; }
    public DateTime? MoveInDate { get; set; }
    public bool Smokes { get; set; }
    public bool OkWithSmoker { get; set; }
    public bool HasPets { get; set; }
    public bool OkWithPets { get; set; }
    public bool NightOwl { get; set; }
    [Range(1, 5)]
    public int CleanlinessLevel { get; set; } = 3;
    public bool IsVisible { get; set; } = true;
}

public class RoommateProfileResponse
{
    public required string UserId { get; set; }
    public string? FullName { get; set; }
    /// <summary>Ghana Card identity verification — the trust signal for moving in with a stranger.</summary>
    public bool IsVerified { get; set; }
    public string? Bio { get; set; }
    public string? University { get; set; }
    public required string PreferredLocation { get; set; }
    public decimal MonthlyBudget { get; set; }
    public DateTime? MoveInDate { get; set; }
    public bool Smokes { get; set; }
    public bool OkWithSmoker { get; set; }
    public bool HasPets { get; set; }
    public bool OkWithPets { get; set; }
    public bool NightOwl { get; set; }
    public int CleanlinessLevel { get; set; }
    public bool IsVisible { get; set; }
}

public class RoommateMatchResponse
{
    public required RoommateProfileResponse Profile { get; set; }
    /// <summary>0–100 compatibility (budget, location, university, habits). Hard conflicts
    /// (smoking/pets intolerance) are excluded from results entirely rather than scored low.</summary>
    public int Score { get; set; }
}
