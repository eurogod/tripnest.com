using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IConversationRepository : IRepository<Conversation>
{
    Task<Conversation?> GetConversationAsync(string userId1, string userId2);
    Task<IEnumerable<Conversation>> GetUserConversationsAsync(string userId);
}
