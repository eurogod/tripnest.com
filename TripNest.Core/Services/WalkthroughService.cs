using Microsoft.AspNetCore.Http;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class WalkthroughService : IWalkthroughService
{
    private static readonly string[] AllowedVideoExtensions = [".mp4", ".mov", ".avi", ".webm", ".mkv"];
    private const long MaxVideoSizeBytes = 500 * 1024 * 1024; // 500 MB

    private readonly IWalkthroughRepository _walkthroughRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<WalkthroughService> _logger;

    public WalkthroughService(
        IWalkthroughRepository walkthroughRepository,
        IPropertyRepository propertyRepository,
        IWebHostEnvironment env,
        ILogger<WalkthroughService> logger)
    {
        _walkthroughRepository = walkthroughRepository;
        _propertyRepository = propertyRepository;
        _env = env;
        _logger = logger;
    }

    public async Task<WalkthroughResponse> UploadWalkthroughAsync(string propertyId, string landlordId, string title, IFormFile videoFile)
    {
        var property = await _propertyRepository.GetByIdAsync(propertyId)
            ?? throw new InvalidOperationException("Property not found");

        if (property.UserId != landlordId)
            throw new InvalidOperationException("You do not own this property");

        if (videoFile.Length == 0)
            throw new InvalidOperationException("Video file is empty");

        if (videoFile.Length > MaxVideoSizeBytes)
            throw new InvalidOperationException("Video file exceeds the 500 MB limit");

        var ext = Path.GetExtension(videoFile.FileName).ToLowerInvariant();
        if (!AllowedVideoExtensions.Contains(ext))
            throw new InvalidOperationException($"Unsupported video format. Allowed: {string.Join(", ", AllowedVideoExtensions)}");

        var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "walkthroughs", propertyId);
        Directory.CreateDirectory(uploadDir);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadDir, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
            await videoFile.CopyToAsync(stream);

        var relativePath = Path.Combine("uploads", "walkthroughs", propertyId, fileName).Replace('\\', '/');

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
            ?? throw new InvalidOperationException("Property not found");

        if (property.WalkthroughStatus != WalkthroughStatus.PendingReview)
            throw new InvalidOperationException("This property does not have a walkthrough pending review");

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
        var allProperties = await _propertyRepository.GetAllAsync();
        return allProperties
            .Where(p => p.WalkthroughStatus == WalkthroughStatus.PendingReview)
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
            ?? throw new InvalidOperationException("Walkthrough not found");
        return MapToResponse(walkthrough);
    }

    public async Task<IEnumerable<WalkthroughResponse>> GetPropertyWalkthroughsAsync(string propertyId)
    {
        var walkthroughs = await _walkthroughRepository.GetByPropertyIdAsync(propertyId);
        return walkthroughs.Select(MapToResponse);
    }

    public async Task DeleteWalkthroughAsync(string walkthroughId)
    {
        var walkthrough = await _walkthroughRepository.GetByIdAsync(walkthroughId)
            ?? throw new InvalidOperationException("Walkthrough not found");

        var fullPath = Path.Combine(_env.WebRootPath, walkthrough.VideoPath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);

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
