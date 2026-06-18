using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IAgentRepository : IRepository<Agent>
{
    Task<Agent?> GetByUserIdAsync(string userId);
}
