using TripNest.Core.DTOs.Roommates;
using TripNest.Core.DTOs.Shared;

namespace TripNest.Core.Interfaces.Services;

public interface IRoommateService
{
    Task<RoommateProfileResponse> UpsertMyProfileAsync(string userId, UpsertRoommateProfileRequest request);
    /// <summary>The caller's own profile; 404 until created.</summary>
    Task<RoommateProfileResponse> GetMyProfileAsync(string userId);
    Task DeleteMyProfileAsync(string userId);
    /// <summary>
    /// Compatible visible profiles, best match first. Requires the caller to have a visible
    /// profile themselves (matching is reciprocal). Hard conflicts (smoking/pets intolerance in
    /// either direction) are excluded outright.
    /// </summary>
    Task<PagedResult<RoommateMatchResponse>> GetMatchesAsync(
        string userId, string? location, decimal? maxBudget, string? university, int page, int pageSize);
}
