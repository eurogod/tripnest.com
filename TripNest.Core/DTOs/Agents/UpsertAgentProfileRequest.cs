namespace TripNest.Core.DTOs.Agents;

/// <summary>
/// Self-service create/update of the caller's public agent directory profile. Without this
/// profile an Agent-role account never appears in <c>GET /api/agents</c>.
/// </summary>
public class UpsertAgentProfileRequest
{
    public required string LicenseNumber { get; set; }

    public required string Bio { get; set; }

    /// <summary>Public contact number. Defaults to the account's phone when omitted.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Commission percentage (0–100).</summary>
    public decimal? CommissionRate { get; set; }

    public int? YearsOfExperience { get; set; }

    public string? Certifications { get; set; }
}
