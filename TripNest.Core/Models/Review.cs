using TripNest.Core.Enums;

namespace TripNest.Core.Models;

public class Review
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string ReviewerId { get; set; }
    public User? Reviewer { get; set; }
    public required string RevieweeId { get; set; }
    public User? Reviewee { get; set; }
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    public int Rating { get; set; }
    public required string Comment { get; set; }
    public ReviewType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
