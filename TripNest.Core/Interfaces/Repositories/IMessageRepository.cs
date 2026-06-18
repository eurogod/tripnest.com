using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IMessageRepository : IRepository<Message>
{
    Task<IEnumerable<Message>> GetByConversationIdAsync(string conversationId);
    Task<IEnumerable<Message>> GetUnreadByUserIdAsync(string userId);
}
