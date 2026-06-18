using TripNest.Core.DTOs.Properties;

namespace TripNest.Core.Interfaces.Services;

public interface IPropertyService
{
    Task<PropertyResponse> CreatePropertyAsync(string userId, CreatePropertyRequest request);
    Task<PropertyResponse> UpdatePropertyAsync(string propertyId, CreatePropertyRequest request);
    Task<PropertyResponse> GetPropertyAsync(string propertyId);
    Task<IEnumerable<PropertyResponse>> GetUserPropertiesAsync(string userId);
    Task<IEnumerable<PropertyResponse>> GetAllActivePropertiesAsync();
    Task<IEnumerable<PropertyResponse>> SearchPropertiesAsync(string location, int minBedrooms, int maxBedrooms);
    Task DeletePropertyAsync(string propertyId);
}
