using Microsoft.EntityFrameworkCore;
using TripNest.Core.Context;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Models;

namespace TripNest.Core.Repositories;

public class ConversationRepository : Repository<Conversation>, IConversationRepository
{
    public ConversationRepository(AppDbContext context) : base(context) { }

    public async Task<Conversation?> GetConversationAsync(string userId1, string userId2)
    {
        return await _context.Set<Conversation>()
            .FirstOrDefaultAsync(c => (c.User1Id == userId1 && c.User2Id == userId2) ||
                                      (c.User1Id == userId2 && c.User2Id == userId1));
    }

    public async Task<IEnumerable<Conversation>> GetUserConversationsAsync(string userId)
    {
        return await _context.Set<Conversation>()
            .Where(c => c.User1Id == userId || c.User2Id == userId)
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .ToListAsync();
    }
}

public class MessageRepository : Repository<Message>, IMessageRepository
{
    public MessageRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Message>> GetByConversationIdAsync(string conversationId)
    {
        return await _context.Set<Message>()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Message>> GetUnreadByUserIdAsync(string userId)
    {
        return await _context.Set<Message>()
            .Where(m => m.Conversation!.User2Id == userId && !m.IsRead)
            .ToListAsync();
    }
}

public class AgentRepository : Repository<Agent>, IAgentRepository
{
    public AgentRepository(AppDbContext context) : base(context) { }

    public async Task<Agent?> GetByUserIdAsync(string userId)
    {
        return await _context.Set<Agent>()
            .FirstOrDefaultAsync(a => a.UserId == userId);
    }
}

public class ReceiptRepository : Repository<Receipt>, IReceiptRepository
{
    public ReceiptRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Receipt>> GetByUserIdAsync(string userId)
    {
        return await _context.Set<Receipt>()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Receipt>> GetByBookingIdAsync(string bookingId)
    {
        return await _context.Set<Receipt>()
            .Where(r => r.BookingId == bookingId)
            .ToListAsync();
    }
}
