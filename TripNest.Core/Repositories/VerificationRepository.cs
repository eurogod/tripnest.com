using Microsoft.EntityFrameworkCore;
using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Models;

namespace TripNest.Core.Repositories;

public class VerificationRepository : Repository<VerificationRequest>, IVerificationRepository
{
    public VerificationRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<VerificationRequest?> GetByUserIdAsync(string userId)
    {
        return await _context.Set<VerificationRequest>()
            .FirstOrDefaultAsync(v => v.UserId == userId);
    }

    public async Task<VerificationRequest?> GetLatestByUserIdAsync(string userId)
    {
        return await _context.Set<VerificationRequest>()
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.SubmittedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetVerifiedCountAsync()
    {
        return await _context.Set<VerificationRequest>()
            .CountAsync(v => v.Status == VerificationStatus.Verified);
    }
}
