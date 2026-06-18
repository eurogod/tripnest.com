namespace TripNest.Core.Models;

public class Receipt
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string BookingId { get; set; }
    public Booking? Booking { get; set; }
    public required string UserId { get; set; }
    public User? User { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
