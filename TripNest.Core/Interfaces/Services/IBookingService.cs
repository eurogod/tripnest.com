using TripNest.Core.DTOs.Bookings;

namespace TripNest.Core.Interfaces.Services;

public interface IBookingService
{
    Task<BookingResponse> CreateBookingAsync(string tenantId, CreateBookingRequest request);
    Task<BookingResponse> GetBookingAsync(string bookingId);
    Task<IEnumerable<BookingResponse>> GetUserBookingsAsync(string tenantId);
    Task<IEnumerable<BookingResponse>> GetPropertyBookingsAsync(string propertyId);
    Task<BookingResponse> CancelBookingAsync(string bookingId);
}
