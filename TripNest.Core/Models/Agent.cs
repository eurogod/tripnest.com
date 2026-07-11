using TripNest.Core.Enums;

namespace TripNest.Core.Models;

public class Agent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string UserId { get; set; }
    public User? User { get; set; }
    public required string LicenseNumber { get; set; }
    public required string PhoneNumber { get; set; }
    public required string Bio { get; set; }
    public AgentStatus Status { get; set; } = AgentStatus.Active;
    public decimal? CommissionRate { get; set; }
    public int? YearsOfExperience { get; set; }
    public DateTime JoinDate { get; set; } = DateTime.UtcNow;
    public string? Certifications { get; set; }
    public string? ServiceArea { get; set; }
}
