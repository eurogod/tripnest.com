using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Agents;

public class AgentResponse
{
    public required string AgentId { get; set; }
    public required string UserId { get; set; }
    public required string LicenseNumber { get; set; }
    public required string PhoneNumber { get; set; }
    public required string Bio { get; set; }
    public AgentStatus Status { get; set; }
    public decimal? CommissionRate { get; set; }
    public int? YearsOfExperience { get; set; }
    public DateTime JoinDate { get; set; }
    public string? Certifications { get; set; }
    public string? ServiceArea { get; set; }
    /// <summary>Mean of viewing-request review ratings; null until the first review.</summary>
    public double? AverageRating { get; set; }
    public int ReviewCount { get; set; }
}
