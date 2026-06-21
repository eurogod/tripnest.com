using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);

    Task<bool> EmailExistsAsync(string email);

    Task<User?> GetByTripNestIdAsync(string tripNestId);

    /// <summary>Count of users that already hold a TripNestId — used as the next serial source.</summary>
    Task<int> CountAssignedTripNestIdsAsync();

    Task<User?> GetByRefreshTokenAsync(string refreshToken);
}
