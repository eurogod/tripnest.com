using Microsoft.AspNetCore.Http;
using TripNest.Core.DTOs.Properties;

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
    Task<IEnumerable<PropertyResponse>> SearchPropertiesAsync(string location, int minBedrooms, int maxBedrooms);
    Task DeletePropertyAsync(string propertyId);
}
