using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IVerificationRepository : IRepository<VerificationRequest>
{
    Task<VerificationRequest?> GetByUserIdAsync(string userId);
    Task<VerificationRequest?> GetLatestByUserIdAsync(string userId);

    Task<int> GetVerifiedCountAsync();

    /// <summary>Counts verification attempts for a user submitted on or after <paramref name="since"/>.</summary>
    Task<int> CountAttemptsSinceAsync(string userId, DateTime since);
}
