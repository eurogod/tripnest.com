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
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<PropertyService> _logger;

    public PropertyService(
        IPropertyRepository propertyRepository,
        IRepository<PropertyPhoto> photoRepository,
        IBookingRepository bookingRepository,
        IFileStorage fileStorage,
        ILogger<PropertyService> logger)
    {
        _propertyRepository = propertyRepository;
        _photoRepository = photoRepository;
        _bookingRepository = bookingRepository;
        _fileStorage = fileStorage;
        _logger = logger;
    }

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
        string location, int minBedrooms, int maxBedrooms, int page, int pageSize)
    {
        var (pageNum, size) = Paging.Clamp(page, pageSize);
        var (items, totalCount) = await _propertyRepository.SearchPageAsync(
            location, minBedrooms, maxBedrooms, pageNum, size);
        return Paging.Result(items.Select(MapToResponse).ToList(), totalCount, pageNum, size);
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
            UpdatedAt = property.UpdatedAt
        };
    }
}
