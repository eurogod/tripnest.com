using TripNest.Core.DTOs.Properties;

namespace TripNest.Core.Interfaces.Services;

public interface IWalkthroughService
{
    Task<WalkthroughResponse> CreateWalkthroughAsync(CreateWalkthroughRequest request);
    Task<WalkthroughResponse> GetWalkthroughAsync(string walkthroughId);
    Task<IEnumerable<WalkthroughResponse>> GetPropertyWalkthroughsAsync(string propertyId);
    Task DeleteWalkthroughAsync(string walkthroughId);
}
