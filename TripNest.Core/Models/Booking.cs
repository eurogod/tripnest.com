using TripNest.Core.Enums;

namespace TripNest.Core.Models;

public class Booking
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string TenantId { get; set; }
    public User? Tenant { get; set; }
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public decimal TotalAmount { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
}
