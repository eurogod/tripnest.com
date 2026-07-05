using Microsoft.AspNetCore.Http;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.DTOs.Shared;

namespace TripNest.Core.Interfaces.Services;

public interface IPropertyService
{
    Task<PropertyResponse> CreatePropertyAsync(string userId, CreatePropertyRequest request);
    /// <summary>Saves uploaded photos for a property (owner only) and returns their stored paths.</summary>
    Task<List<string>> AddPhotosAsync(string propertyId, string userId, IFormFileCollection files);
    Task<PropertyResponse> UpdatePropertyAsync(string propertyId, CreatePropertyRequest request);
    Task<PropertyResponse> GetPropertyAsync(string propertyId);
    Task<IEnumerable<PropertyResponse>> GetUserPropertiesAsync(string userId);
    Task<IEnumerable<PropertyResponse>> GetAllActivePropertiesAsync();
    Task<PagedResult<PropertyResponse>> SearchPropertiesAsync(string location, int minBedrooms, int maxBedrooms, int page, int pageSize);
    Task DeletePropertyAsync(string propertyId);
}
