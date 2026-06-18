namespace TripNest.Core.Models;

public class WishlistItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string UserId { get; set; }
    public User? User { get; set; }
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
