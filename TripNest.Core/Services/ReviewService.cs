using TripNest.Core.DTOs.Reviews;
using TripNest.Core.Exceptions;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class ReviewService : IReviewService
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(
        IReviewRepository reviewRepository,
        IBookingRepository bookingRepository,
        ILogger<ReviewService> logger)
    {
        _reviewRepository = reviewRepository;
        _bookingRepository = bookingRepository;
        _logger = logger;
    }

    public async Task<ReviewResponse> CreateReviewAsync(
        string bookingId,
        string propertyId,
        string reviewerId,
        int rating,
        string? comment)
    {
        try
        {
            var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId);
            if (booking == null)
                throw new NotFoundException("Booking");

            // Review integrity: only the tenant who actually stayed may review, and only the
            // property that booking was for — otherwise anyone holding a completed booking id
            // could plant reviews on arbitrary listings or on someone else's stay.
            if (booking.TenantId != reviewerId)
                throw new ForbiddenException("Only the booking's tenant can review this stay");

            if (booking.PropertyId != propertyId)
                throw new NotFoundException("A booking for this property");

            if (booking.Status != BookingStatus.Completed)
                throw new ValidationException("Reviews can only be submitted for completed bookings");

            var existing = await _reviewRepository.GetByPropertyIdAsync(propertyId);
            var duplicate = existing.Any(r => r.ReviewerId == reviewerId);
            if (duplicate)
                throw new ConflictException("You have already reviewed this property");

            // Derive landlord id from the booking's property navigation property
            var landlordId = booking.Property?.UserId
                ?? throw new NotFoundException("Booking property details");

            var review = new Review
            {
                ReviewerId = reviewerId,
                RevieweeId = landlordId,
                PropertyId = propertyId,
                Rating = rating,
                Comment = comment ?? "",
                Type = ReviewType.Property
            };

            await _reviewRepository.AddAsync(review);
            await _reviewRepository.SaveChangesAsync();

            _logger.LogInformation("Review created: {ReviewId} for property {PropertyId}", review.Id, propertyId);

            return MapToResponse(review);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review for booking {BookingId}", bookingId);
            throw;
        }
    }

    public async Task<PagedResult<ReviewResponse>> GetPropertyReviewsAsync(string propertyId, int page, int pageSize)
    {
        // Page in the database — popular properties accumulate reviews without bound.
        var (pageNum, size) = Paging.Clamp(page, pageSize);
        var (items, totalCount) = await _reviewRepository.FindPageAsync(
            r => r.PropertyId == propertyId,
            q => q.OrderByDescending(r => r.CreatedAt),
            pageNum, size);

        return Paging.Result(items.Select(MapToResponse).ToList(), totalCount, pageNum, size);
    }

    public async Task<PagedResult<ReviewResponse>> GetUserReviewsAsync(string userId, int page, int pageSize)
    {
        var reviews = await _reviewRepository.GetByRevieweeIdAsync(userId);
        return Paging.Page(reviews.Select(MapToResponse).ToList(), page, pageSize);
    }

    public async Task<ReviewResponse?> GetReviewAsync(string reviewId)
    {
        var review = await _reviewRepository.GetByIdAsync(reviewId);
        return review == null ? null : MapToResponse(review);
    }

    public async Task DeleteReviewAsync(string reviewId, string userId)
    {
        try
        {
            var review = await _reviewRepository.GetByIdAsync(reviewId);
            if (review == null)
                throw new NotFoundException("Review");

            if (review.ReviewerId != userId)
                throw new ForbiddenException("You are not authorised to delete this review");

            await _reviewRepository.DeleteAsync(review);
            await _reviewRepository.SaveChangesAsync();

            _logger.LogInformation("Review deleted: {ReviewId} by user {UserId}", reviewId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting review {ReviewId}", reviewId);
            throw;
        }
    }

    private static ReviewResponse MapToResponse(Review r) => new ReviewResponse
    {
        ReviewId = r.Id,
        ReviewerId = r.ReviewerId,
        RevieweeId = r.RevieweeId,
        PropertyId = r.PropertyId,
        Rating = r.Rating,
        Comment = r.Comment,
        Type = r.Type,
        CreatedAt = r.CreatedAt
    };
}
