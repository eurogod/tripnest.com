using TripNest.Core.DTOs.Properties;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class WalkthroughService : IWalkthroughService
{
    private readonly IWalkthroughRepository _walkthroughRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly ILogger<WalkthroughService> _logger;

    public WalkthroughService(
        IWalkthroughRepository walkthroughRepository,
        IPropertyRepository propertyRepository,
        ILogger<WalkthroughService> logger)
    {
        _walkthroughRepository = walkthroughRepository;
        _propertyRepository = propertyRepository;
        _logger = logger;
    }

    public async Task<WalkthroughResponse> CreateWalkthroughAsync(CreateWalkthroughRequest request)
    {
        try
        {
            var property = await _propertyRepository.GetByIdAsync(request.PropertyId);
            if (property == null)
                throw new InvalidOperationException("Property not found");

            var walkthrough = new Walkthrough
            {
                PropertyId = request.PropertyId,
                Title = request.Title,
                VideoUrl = request.VideoUrl,
                ThumbnailUrl = request.ThumbnailUrl,
                DurationSeconds = request.DurationSeconds
            };

            await _walkthroughRepository.AddAsync(walkthrough);
            await _walkthroughRepository.SaveChangesAsync();

            _logger.LogInformation("Walkthrough created: {WalkthroughId} for property {PropertyId}", walkthrough.Id, request.PropertyId);

            return MapToResponse(walkthrough);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating walkthrough");
            throw;
        }
    }

    public async Task<WalkthroughResponse> GetWalkthroughAsync(string walkthroughId)
    {
        try
        {
            var walkthrough = await _walkthroughRepository.GetByIdAsync(walkthroughId);
            if (walkthrough == null)
                throw new InvalidOperationException("Walkthrough not found");

            return MapToResponse(walkthrough);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving walkthrough");
            throw;
        }
    }

    public async Task<IEnumerable<WalkthroughResponse>> GetPropertyWalkthroughsAsync(string propertyId)
    {
        try
        {
            var walkthroughs = await _walkthroughRepository.GetByPropertyIdAsync(propertyId);
            return walkthroughs.Select(MapToResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving property walkthroughs");
            throw;
        }
    }

    public async Task DeleteWalkthroughAsync(string walkthroughId)
    {
        try
        {
            var walkthrough = await _walkthroughRepository.GetByIdAsync(walkthroughId);
            if (walkthrough == null)
                throw new InvalidOperationException("Walkthrough not found");

            await _walkthroughRepository.DeleteAsync(walkthrough);
            await _walkthroughRepository.SaveChangesAsync();

            _logger.LogInformation("Walkthrough deleted: {WalkthroughId}", walkthroughId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting walkthrough");
            throw;
        }
    }

    private WalkthroughResponse MapToResponse(Walkthrough walkthrough)
    {
        return new WalkthroughResponse
        {
            WalkthroughId = walkthrough.Id,
            PropertyId = walkthrough.PropertyId,
            Title = walkthrough.Title,
            VideoUrl = walkthrough.VideoUrl,
            ThumbnailUrl = walkthrough.ThumbnailUrl,
            DurationSeconds = walkthrough.DurationSeconds,
            CreatedAt = walkthrough.CreatedAt
        };
    }
}
