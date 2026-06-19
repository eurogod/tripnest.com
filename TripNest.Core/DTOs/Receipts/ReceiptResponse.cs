namespace TripNest.Core.DTOs.Receipts;

public class ReceiptResponse
{
    public required string ReceiptId { get; set; }
    public required string BookingId { get; set; }
    public required string UserId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; }
}
