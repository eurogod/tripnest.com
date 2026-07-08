using Microsoft.AspNetCore.Http;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.DTOs.Shared;

namespace TripNest.Core.Interfaces.Services;

public interface IPropertyService
{
    Task<PropertyResponse> CreatePropertyAsync(string userId, CreatePropertyRequest request);
    /// <summary>Saves uploaded photos for a property (owner only) and returns their stored paths.</summary>
    Task<List<string>> AddPhotosAsync(string propertyId, string userId, IFormFileCollection files);
    /// <summary>Generates AI listing copy (title, description, highlights) for the owner's property.</summary>
    Task<ListingCopySuggestion> GenerateListingCopyAsync(string propertyId, string userId);
    /// <summary>Owner/admin only.</summary>
    Task<PropertyResponse> UpdatePropertyAsync(string propertyId, string userId, bool isAdmin, CreatePropertyRequest request);
    Task<PropertyResponse> GetPropertyAsync(string propertyId);
    Task<IEnumerable<PropertyResponse>> GetUserPropertiesAsync(string userId);
    Task<IEnumerable<PropertyResponse>> GetAllActivePropertiesAsync();
    Task<PagedResult<PropertyResponse>> SearchPropertiesAsync(string location, int minBedrooms, int maxBedrooms, int page, int pageSize);
    /// <summary>Owner/admin only. Hard-deletes a never-booked listing; archives one with booking
    /// history (escrow audit events are delete-restricted). Returns true when hard-deleted.</summary>
    Task<bool> DeletePropertyAsync(string propertyId, string userId, bool isAdmin);
}
