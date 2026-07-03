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

    public async Task<IReadOnlyList<Agent>> SearchByNameAsync(string? query, int take)
    {
        var q = _context.Set<Agent>().AsNoTracking().Include(a => a.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var ql = query.ToLower();
            q = q.Where(a => a.User != null && a.User.FullName.ToLower().Contains(ql));
        }
        return await q.Take(take).ToListAsync();
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

    public async Task<IEnumerable<Receipt>> GetByBookingIdsAsync(IEnumerable<string> bookingIds)
    {
        var ids = bookingIds.ToList();
        if (ids.Count == 0)
            return new List<Receipt>();

        return await _context.Set<Receipt>()
            .Where(r => ids.Contains(r.BookingId))
            .ToListAsync();
    }
}
