using Microsoft.EntityFrameworkCore;
using TripNest.Core.Context;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Models;

namespace TripNest.Core.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _dbSet.AnyAsync(u => u.Email == email);
    }

    public async Task<User?> GetByTripNestIdAsync(string tripNestId)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.TripNestId == tripNestId);
    }

    public async Task<int> CountAssignedTripNestIdsAsync()
    {
        return await _dbSet.CountAsync(u => u.TripNestId != null);
    }

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && u.RefreshTokenExpiryTime > DateTime.UtcNow);
    }
}
