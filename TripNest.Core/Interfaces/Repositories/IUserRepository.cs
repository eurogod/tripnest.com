using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);

    Task<bool> EmailExistsAsync(string email);

    Task<User?> GetByTripNestIdAsync(string tripNestId);

    Task<User?> GetByRefreshTokenAsync(string refreshToken);
}
