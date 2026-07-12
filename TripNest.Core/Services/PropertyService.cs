using Microsoft.AspNetCore.Http;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class PropertyService : IPropertyService
{
    private readonly IPropertyRepository _propertyRepository;
    private readonly IRepository<PropertyPhoto> _photoRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IRepository<PricingSettings> _pricingRepository;
    private readonly IStayDiscountService _stayDiscountService;
    private readonly IDynamicPricingService _dynamicPricingService;
    private readonly IConfiguration _configuration;
    private readonly IFileStorage _fileStorage;
    private readonly IAiClient _aiClient;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<PropertyService> _logger;

    public PropertyService(
        IPropertyRepository propertyRepository,
        IRepository<PropertyPhoto> photoRepository,
        IBookingRepository bookingRepository,
        IRepository<PricingSettings> pricingRepository,
        IStayDiscountService stayDiscountService,
        IDynamicPricingService dynamicPricingService,
        IConfiguration configuration,
        IFileStorage fileStorage,
        IAiClient aiClient,
        IUserRepository userRepository,
        ILogger<PropertyService> logger)
    {
        _propertyRepository = propertyRepository;
        _photoRepository = photoRepository;
        _bookingRepository = bookingRepository;
        _pricingRepository = pricingRepository;
        _stayDiscountService = stayDiscountService;
        _dynamicPricingService = dynamicPricingService;
        _configuration = configuration;
        _fileStorage = fileStorage;
        _aiClient = aiClient;
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Drafts AI listing copy for the host to review — never applied automatically. Feeds the
    /// model the property's structured facts plus up to four listing photos.
    /// </summary>
    public async Task<ListingCopySuggestion> GenerateListingCopyAsync(string propertyId, string userId)
    {
        var property = await _propertyRepository.GetByIdAsync(propertyId)
            ?? throw new NotFoundException("Property");
        if (property.UserId != userId)
            throw new ForbiddenException("You are not authorised to generate copy for this property");

        if (!_aiClient.IsConfigured)
            throw new ValidationException("AI listing suggestions are not configured on this server.");

        var photos = await LoadPhotosForAiAsync(propertyId);
        var owner = await _userRepository.GetByIdAsync(userId);
        var language = owner?.PreferredLanguage ?? Enums.Language.English;
        var suggestion = await _aiClient.GenerateListingCopyAsync(property, photos, language);
        return suggestion
            ?? throw new ValidationException("Listing suggestions are unavailable right now. Please try again.");
    }

    // Claude accepts up to ~5MB per image; anything larger is skipped rather than resized —
    // listing photos are usually smaller, and copy quality barely depends on any single one.
    private const int MaxAiPhotos = 4;
    private const long MaxAiPhotoBytes = 4_500_000;

    private async Task<IReadOnlyList<AiImage>> LoadPhotosForAiAsync(string propertyId)
    {
        var photoRows = (await _photoRepository.FindAsync(p => p.PropertyId == propertyId))
            .OrderByDescending(p => p.IsPrimary)
            .ThenBy(p => p.SortOrder)
            .Take(MaxAiPhotos)
            .ToList();

        var images = new List<AiImage>();
        foreach (var row in photoRows)
        {
            var mediaType = MediaTypeFor(row.PhotoPath);
            if (mediaType is null)
                continue;

            await using var stream = await _fileStorage.OpenReadAsync(row.PhotoPath);
            if (stream is null)
                continue;

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            if (buffer.Length == 0 || buffer.Length > MaxAiPhotoBytes)
                continue;

            images.Add(new AiImage(buffer.ToArray(), mediaType));
        }
        return images;
    }

    private static string? MediaTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => null,
    };

    public async Task<List<string>> AddPhotosAsync(string propertyId, string userId, IFormFileCollection files)
    {
        var property = await _propertyRepository.GetByIdAsync(propertyId)
            ?? throw new NotFoundException("Property");
        if (property.UserId != userId)
            throw new ForbiddenException("You are not authorised to add photos to this property");
        if (files == null || files.Count == 0)
            throw new ValidationException("No photos were provided");

        // Count existing photos in the database (don't load the whole table).
        var existing = await _photoRepository.CountAsync(p => p.PropertyId == propertyId);
        var savedPaths = new List<string>();
        var index = existing;

        foreach (var file in files)
        {
            if (file.Length == 0) continue;

            // Storage validates type + size and returns a servable path/URL.
            var relativePath = await _fileStorage.SaveAsync("properties", file, UploadKind.Image);
            await _photoRepository.AddAsync(new PropertyPhoto
            {
                PropertyId = propertyId,
                PhotoPath = relativePath,
                IsPrimary = index == 0,
                SortOrder = index
            });
            savedPaths.Add(relativePath);
            index++;
        }

        await _photoRepository.SaveChangesAsync();
        _logger.LogInformation("Added {Count} photo(s) to property {PropertyId}", savedPaths.Count, propertyId);
        return savedPaths;
    }

    public async Task<PropertyResponse> CreatePropertyAsync(string userId, CreatePropertyRequest request)
    {
        try
        {
            var property = new Property
            {
                UserId = userId,
                Title = request.Title,
                Description = request.Description,
                Location = request.Location,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Bedrooms = request.Bedrooms,
                Bathrooms = request.Bathrooms,
                MonthlyRent = request.MonthlyRent,
                DailyRate = request.DailyRate,
                PropertyType = request.PropertyType,
                StayType = request.StayType,
                CancellationPolicy = request.CancellationPolicy,
                Amenities = request.Amenities,
                Status = PropertyStatus.Draft
            };

            await _propertyRepository.AddAsync(property);
            await _propertyRepository.SaveChangesAsync();

            _logger.LogInformation("Property created: {PropertyId} for user {UserId}", property.Id, userId);

            return MapToResponse(property);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating property");
            throw;
        }
    }

    public async Task<PropertyResponse> UpdatePropertyAsync(string propertyId, string userId, bool isAdmin, CreatePropertyRequest request)
    {
        try
        {
            var property = await _propertyRepository.GetByIdAsync(propertyId)
                ?? throw new NotFoundException("Property");

            // Only the owner (or an admin) may edit a listing.
            if (property.UserId != userId && !isAdmin)
                throw new ForbiddenException("This property is not yours");

            property.Title = request.Title;
            property.Description = request.Description;
            property.Location = request.Location;
            property.Latitude = request.Latitude;
            property.Longitude = request.Longitude;
            property.Bedrooms = request.Bedrooms;
            property.Bathrooms = request.Bathrooms;
            property.MonthlyRent = request.MonthlyRent;
            property.DailyRate = request.DailyRate;
            property.PropertyType = request.PropertyType;
            property.StayType = request.StayType;
            property.CancellationPolicy = request.CancellationPolicy;
            property.Amenities = request.Amenities;
            property.UpdatedAt = DateTime.UtcNow;

            await _propertyRepository.UpdateAsync(property);
            await _propertyRepository.SaveChangesAsync();

            _logger.LogInformation("Property updated: {PropertyId}", propertyId);

            return MapToResponse(property);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating property");
            throw;
        }
    }

    public async Task<PropertyResponse> GetPropertyAsync(string propertyId)
    {
        try
        {
            var property = await _propertyRepository.GetByIdAsync(propertyId);
            if (property == null)
                throw new InvalidOperationException("Property not found");

            return MapToResponse(property);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving property");
            throw;
        }
    }

    public async Task<IEnumerable<PropertyResponse>> GetUserPropertiesAsync(string userId)
    {
        try
        {
            var properties = await _propertyRepository.GetByUserIdAsync(userId);
            return properties.Select(MapToResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user properties");
            throw;
        }
    }

    public async Task<IEnumerable<PropertyResponse>> GetAllActivePropertiesAsync()
    {
        try
        {
            var properties = await _propertyRepository.GetAllActiveAsync();
            return properties.Select(MapToResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active properties");
            throw;
        }
    }

    public async Task<PagedResult<PropertyResponse>> SearchPropertiesAsync(
        DTOs.Search.PropertySearchCriteria criteria, int page, int pageSize)
    {
        var (pageNum, size) = Paging.Clamp(page, pageSize);
        var (items, totalCount) = await _propertyRepository.SearchPageAsync(criteria, pageNum, size);
        var responses = items.Select(MapToResponse).ToList();

        // True total pricing: when the search carries dates, each result gets the exact all-in
        // cost for that stay (weekend rates, length discounts, cleaning fee) — the same number the
        // booking will charge — so there is never a surprise fee at checkout.
        if (criteria.HasDates && items.Count > 0)
        {
            var ids = items.Select(p => p.Id).ToList();
            var pricingById = (await _pricingRepository.FindAsync(s => ids.Contains(s.PropertyId)))
                .ToDictionary(s => s.PropertyId);
            foreach (var (property, response) in items.Zip(responses))
            {
                var adjusted = await _dynamicPricingService.AdjustAsync(
                    property, pricingById.GetValueOrDefault(property.Id),
                    criteria.CheckIn!.Value, criteria.CheckOut!.Value);
                response.Quote = StayPricingCalculator.Quote(
                    property, adjusted, criteria.CheckIn!.Value, criteria.CheckOut!.Value);
            }
        }

        return Paging.Result(responses, totalCount, pageNum, size);
    }

    /// <summary>
    /// All-in price breakdown for a stay on one listing — the number the booking will charge.
    /// Pass the caller's user id to include their loyalty discount; anonymous quotes get none.
    /// </summary>
    public async Task<StayQuote> GetStayQuoteAsync(string propertyId, DateTime checkIn, DateTime checkOut, string? userId)
    {
        if (checkOut.Date <= checkIn.Date)
            throw new ValidationException("Check-out date must be after the check-in date");

        var property = await _propertyRepository.GetByIdAsync(propertyId)
            ?? throw new NotFoundException("Property");

        var pricing = (await _pricingRepository.FindAsync(s => s.PropertyId == propertyId)).FirstOrDefault();
        pricing = await _dynamicPricingService.AdjustAsync(property, pricing, checkIn, checkOut);
        var memberPercent = string.IsNullOrEmpty(userId)
            ? 0m
            : await _stayDiscountService.GetPercentAsync(userId, property);
        return StayPricingCalculator.Quote(property, pricing, checkIn, checkOut, memberPercent);
    }

    /// <summary>Deletes a never-booked listing outright; archives one with booking history.
    /// Returns true when hard-deleted, false when archived.</summary>
    public async Task<bool> DeletePropertyAsync(string propertyId, string userId, bool isAdmin)
    {
        var property = await _propertyRepository.GetByIdAsync(propertyId)
            ?? throw new NotFoundException("Property");

        // Only the owner (or an admin) may remove a listing.
        if (property.UserId != userId && !isAdmin)
            throw new ForbiddenException("This property is not yours");

        // A property with booking history can't be hard-deleted: the cascade would reach escrows,
        // whose audit events are deliberately delete-restricted (money history must survive).
        // Archive it instead — it leaves search/booking (only Active listings surface) while
        // bookings, escrows, and agreements keep valid references.
        var hasBookings = (await _bookingRepository.FindAsync(b => b.PropertyId == propertyId)).Any();
        if (hasBookings)
        {
            property.Status = PropertyStatus.Archived;
            await _propertyRepository.UpdateAsync(property);
            await _propertyRepository.SaveChangesAsync();
            _logger.LogInformation("Property archived (has booking history): {PropertyId}", propertyId);
            return false;
        }

        await _propertyRepository.DeleteAsync(property);
        await _propertyRepository.SaveChangesAsync();
        _logger.LogInformation("Property deleted: {PropertyId}", propertyId);
        return true;
    }

    private PropertyResponse MapToResponse(Property property)
    {
        return new PropertyResponse
        {
            PropertyId = property.Id,
            OwnerId = property.UserId,
            Title = property.Title,
            Description = property.Description,
            Location = property.Location,
            Latitude = property.Latitude,
            Longitude = property.Longitude,
            Bedrooms = property.Bedrooms,
            Bathrooms = property.Bathrooms,
            MonthlyRent = property.MonthlyRent,
            DailyRate = property.DailyRate,
            PropertyType = property.PropertyType,
            StayType = property.StayType,
            CancellationPolicy = property.CancellationPolicy,
            Amenities = property.Amenities,
            PhotoPaths = property.PhotoPaths,
            Status = property.Status,
            CreatedAt = property.CreatedAt,
            UpdatedAt = property.UpdatedAt,
            WalkthroughVerifiedAt = property.WalkthroughReviewedAt,
            WalkthroughBadgeFresh = property.WalkthroughStatus == Enums.WalkthroughStatus.Approved &&
                                    property.WalkthroughReviewedAt is { } reviewed &&
                                    reviewed > DateTime.UtcNow.AddDays(-_configuration.GetValue("Walkthrough:BadgeValidityDays", 365))
        };
    }
}
