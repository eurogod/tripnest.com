using TripNest.Core.DTOs.Bookings;

namespace TripNest.Core.Interfaces.Services;

public interface IBookingService
{
    Task<BookingResponse> CreateBookingAsync(string tenantId, CreateBookingRequest request);
    Task<BookingResponse> GetBookingAsync(string bookingId, string userId);
    Task<IEnumerable<BookingResponse>> GetUserBookingsAsync(string tenantId);
    Task<BookingResponse> CancelBookingAsync(string bookingId, string userId);
}
