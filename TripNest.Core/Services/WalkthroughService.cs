using Microsoft.AspNetCore.Http;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class WalkthroughService : IWalkthroughService
{
    private readonly IWalkthroughRepository _walkthroughRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<WalkthroughService> _logger;

    public WalkthroughService(
        IWalkthroughRepository walkthroughRepository,
        IPropertyRepository propertyRepository,
        IFileStorage fileStorage,
        ILogger<WalkthroughService> logger)
    {
        _walkthroughRepository = walkthroughRepository;
        _propertyRepository = propertyRepository;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<WalkthroughResponse> UploadWalkthroughAsync(string propertyId, string landlordId, string title, IFormFile videoFile)
    {
        var property = await _propertyRepository.GetByIdAsync(propertyId)
            ?? throw new NotFoundException("Property");

        if (property.UserId != landlordId)
            throw new ForbiddenException("You do not own this property");

        // Storage validates type + size and returns a servable path/URL.
        var relativePath = await _fileStorage.SaveAsync($"walkthroughs/{propertyId}", videoFile, UploadKind.Video);

        var walkthrough = new Walkthrough
        {
            PropertyId = propertyId,
            Title = title,
            VideoPath = relativePath
        };

        await _walkthroughRepository.AddAsync(walkthrough);

        // Update the property approval gate
        property.WalkthroughVideoPath = relativePath;
        property.WalkthroughStatus = WalkthroughStatus.PendingReview;
        await _propertyRepository.UpdateAsync(property);
        await _walkthroughRepository.SaveChangesAsync();

        _logger.LogInformation("Walkthrough uploaded for property {PropertyId}: {FilePath}", propertyId, relativePath);

        return MapToResponse(walkthrough);
    }

    public async Task<PropertyWalkthroughStatusResponse> ReviewWalkthroughAsync(string propertyId, string reviewerId, bool approved, string? rejectionReason)
    {
        var property = await _propertyRepository.GetByIdAsync(propertyId)
            ?? throw new NotFoundException("Property");

        if (property.WalkthroughStatus != WalkthroughStatus.PendingReview)
            throw new ValidationException("This property does not have a walkthrough pending review");

        property.WalkthroughStatus = approved ? WalkthroughStatus.Approved : WalkthroughStatus.Rejected;
        property.WalkthroughReviewedById = reviewerId;
        property.WalkthroughReviewedAt = DateTime.UtcNow;
        property.WalkthroughRejectionReason = approved ? null : rejectionReason;

        await _propertyRepository.UpdateAsync(property);
        await _propertyRepository.SaveChangesAsync();

        _logger.LogInformation("Walkthrough {Status} for property {PropertyId} by reviewer {ReviewerId}", property.WalkthroughStatus, propertyId, reviewerId);

        return new PropertyWalkthroughStatusResponse
        {
            PropertyId = property.Id,
            WalkthroughStatus = property.WalkthroughStatus,
            VideoPath = property.WalkthroughVideoPath,
            RejectionReason = property.WalkthroughRejectionReason,
            ReviewedAt = property.WalkthroughReviewedAt
        };
    }

    public async Task<IEnumerable<PropertyWalkthroughStatusResponse>> GetPendingWalkthroughsAsync()
    {
        var pending = await _propertyRepository.FindAsync(p => p.WalkthroughStatus == WalkthroughStatus.PendingReview);
        return pending
            .Select(p => new PropertyWalkthroughStatusResponse
            {
                PropertyId = p.Id,
                WalkthroughStatus = p.WalkthroughStatus,
                VideoPath = p.WalkthroughVideoPath,
                RejectionReason = p.WalkthroughRejectionReason,
                ReviewedAt = p.WalkthroughReviewedAt
            });
    }

    public async Task<WalkthroughResponse> GetWalkthroughAsync(string walkthroughId)
    {
        var walkthrough = await _walkthroughRepository.GetByIdAsync(walkthroughId)
            ?? throw new NotFoundException("Walkthrough");
        return MapToResponse(walkthrough);
    }

    public async Task<IEnumerable<WalkthroughResponse>> GetPropertyWalkthroughsAsync(string propertyId)
    {
        var walkthroughs = await _walkthroughRepository.GetByPropertyIdAsync(propertyId);
        return walkthroughs.Select(MapToResponse);
    }

    public async Task DeleteWalkthroughAsync(string propertyId, string walkthroughId, string userId, bool isAdmin)
    {
        var walkthrough = await _walkthroughRepository.GetByIdAsync(walkthroughId)
            ?? throw new NotFoundException("Walkthrough");

        // The walkthrough must belong to the property in the route (no cross-property deletes)...
        if (walkthrough.PropertyId != propertyId)
            throw new NotFoundException("Walkthrough");

        // ...and only the property's owner (or an admin) may delete it. Without this any verified
        // landlord could delete another landlord's walkthrough video (and its stored file) by id.
        if (!isAdmin)
        {
            var property = await _propertyRepository.GetByIdAsync(propertyId)
                ?? throw new NotFoundException("Property");
            if (property.UserId != userId)
                throw new ForbiddenException("You do not own this property");
        }

        await _fileStorage.DeleteAsync(walkthrough.VideoPath);

        await _walkthroughRepository.DeleteAsync(walkthrough);
        await _walkthroughRepository.SaveChangesAsync();
    }

    private static WalkthroughResponse MapToResponse(Walkthrough w) => new()
    {
        WalkthroughId = w.Id,
        PropertyId = w.PropertyId,
        Title = w.Title,
        VideoPath = w.VideoPath,
        ThumbnailUrl = w.ThumbnailUrl,
        DurationSeconds = w.DurationSeconds,
        CreatedAt = w.CreatedAt
    };
}
