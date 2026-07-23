using Microsoft.AspNetCore.Http;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Enums;

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
    Task<PagedResult<PropertyResponse>> SearchPropertiesAsync(DTOs.Search.PropertySearchCriteria criteria, int page, int pageSize);
    /// <summary>All-in price breakdown for a stay; includes the caller's loyalty discount when authenticated.</summary>
    Task<StayQuote> GetStayQuoteAsync(string propertyId, DateTime checkIn, DateTime checkOut, string? userId);
    /// <summary>Owner/admin only. Hard-deletes a never-booked listing; archives one with booking
    /// history (escrow audit events are delete-restricted). Returns true when hard-deleted.</summary>
    Task<bool> DeletePropertyAsync(string propertyId, string userId, bool isAdmin);
    /// <summary>Owner/admin only. Publishes a listing (Active) or takes it offline (Inactive) /
    /// back to Draft, instantly — no admin approval or walkthrough gate.</summary>
    Task<PropertyResponse> SetStatusAsync(string propertyId, string userId, bool isAdmin, PropertyStatus status);
    /// <summary>Owner/admin only. Marks one uploaded photo as the listing's cover (primary).</summary>
    Task<PropertyResponse> SetCoverPhotoAsync(string propertyId, string userId, bool isAdmin, string photoId);
    /// <summary>Owner/admin only. Removes one uploaded photo; promotes another to cover if needed.</summary>
    Task<PropertyResponse> RemovePhotoAsync(string propertyId, string userId, bool isAdmin, string photoId);
}
