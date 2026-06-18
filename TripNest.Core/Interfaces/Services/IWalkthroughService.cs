using Microsoft.AspNetCore.Http;
using TripNest.Core.DTOs.Properties;

namespace TripNest.Core.Interfaces.Services;

public interface IWalkthroughService
{
    Task<WalkthroughResponse> UploadWalkthroughAsync(string propertyId, string landlordId, string title, IFormFile videoFile);
    Task<WalkthroughResponse> GetWalkthroughAsync(string walkthroughId);
    Task<IEnumerable<WalkthroughResponse>> GetPropertyWalkthroughsAsync(string propertyId);
    Task<PropertyWalkthroughStatusResponse> ReviewWalkthroughAsync(string propertyId, string reviewerId, bool approved, string? rejectionReason);
    Task<IEnumerable<PropertyWalkthroughStatusResponse>> GetPendingWalkthroughsAsync();
    Task DeleteWalkthroughAsync(string walkthroughId);
}
