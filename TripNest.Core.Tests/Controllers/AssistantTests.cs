using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// The TripNest Assistant: grounded Q&amp;A, admin escalation via support tickets, graceful
/// degradation when AI is unconfigured, and the admin resolve flow.
/// </summary>
public class AssistantTests : TestBase
{
    [Fact]
    public async Task Ask_ReturnsAnswer_AndPersistsHistory()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);

        var stub = _fixture.Services.GetRequiredService<StubAiClient>();
        stub.Configured = true;
        stub.NextCompletion = """{"answer": "Your payment is held in escrow until 24h after checkout.", "escalate": false}""";

        var res = await _httpClient.PostAsJsonAsync("/api/assistant/ask", new { question = "How does escrow work?" });
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.OK, $"Expected OK but got {res.StatusCode}: {body}");
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");
        Assert.Contains("escrow", data.GetProperty("answer").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.False(data.GetProperty("escalated").GetBoolean());

        // Both turns (question + answer) are persisted.
        var history = await _httpClient.GetAsync("/api/assistant/history");
        var items = JsonDocument.Parse(await history.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.Equal(2, items.GetArrayLength());
    }

    [Fact]
    public async Task Ask_WhenModelEscalates_CreatesTicketAndNotifiesAdmin()
    {
        // An admin must exist to receive the escalation notification.
        string adminId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = new User
            {
                FullName = "Admin", Email = $"admin_{Guid.NewGuid():N}@tripnest.local",
                PasswordHash = "x", Phone = "+233500000000", Role = UserRole.Admin, IsActive = true,
            };
            db.Set<User>().Add(admin);
            await db.SaveChangesAsync();
            adminId = admin.Id;
        }

        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        var stub = _fixture.Services.GetRequiredService<StubAiClient>();
        stub.Configured = true;
        stub.NextCompletion = """
            {"answer": "I've flagged this for our team.", "escalate": true,
             "escalationSubject": "Disputed refund", "escalationSummary": "Guest says refund never arrived."}
            """;

        var res = await _httpClient.PostAsJsonAsync("/api/assistant/ask",
            new { question = "My refund never came and the host is ignoring me!" });
        var data = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        Assert.True(data.GetProperty("escalated").GetBoolean());
        var ticketId = data.GetProperty("supportTicketId").GetString();
        Assert.False(string.IsNullOrEmpty(ticketId));

        // A live chat with the admin was opened — the user is "connected to support".
        var conversationId = data.GetProperty("supportConversationId").GetString();
        Assert.False(string.IsNullOrEmpty(conversationId));

        // The ticket exists and links to that conversation, the admin got a notification, and
        // the conversation is seeded with the user's issue.
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ticket = await db.Set<SupportTicket>().FindAsync(ticketId);
            Assert.NotNull(ticket);
            Assert.Equal(SupportTicketStatus.Open, ticket!.Status);
            Assert.Equal(userId, ticket.UserId);
            Assert.Equal(conversationId, ticket.ConversationId);

            var conversation = await db.Set<Conversation>().FindAsync(conversationId);
            Assert.NotNull(conversation);
            Assert.True(
                (conversation!.User1Id == userId && conversation.User2Id == adminId) ||
                (conversation.User1Id == adminId && conversation.User2Id == userId),
                "conversation should be between the user and the admin");
            Assert.Contains(db.Set<Message>(), m => m.ConversationId == conversationId && m.SenderId == userId);

            Assert.True(db.Set<Notification>().Any(n => n.UserId == adminId),
                "admin should have been notified of the escalation");
        }
    }

    [Fact]
    public async Task Ask_RespondsInUsersPreferredLanguage()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        // Set the user's language to French.
        await _httpClient.PutAsJsonAsync("/api/profile/me", new { preferredLanguage = (int)Language.French });

        var stub = _fixture.Services.GetRequiredService<StubAiClient>();
        stub.Configured = true;
        stub.NextCompletion = """{"answer": "Bonjour! Voici comment fonctionne le séquestre.", "escalate": false}""";

        await _httpClient.PostAsJsonAsync("/api/assistant/ask", new { question = "Comment ça marche?" });

        // The system prompt sent to the model names the user's language.
        var lastCompletion = stub.Completions.Last();
        Assert.Contains("French", lastCompletion.SystemPrompt);
    }

    [Fact]
    public async Task Ask_WhenAiNotConfigured_Returns400()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        var stub = _fixture.Services.GetRequiredService<StubAiClient>();
        stub.Configured = false;

        var res = await _httpClient.PostAsJsonAsync("/api/assistant/ask", new { question = "hello" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Admin_CanListAndResolveEscalatedTickets()
    {
        // Seed a user + an open ticket directly.
        string ticketId, ticketUserId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new User
            {
                FullName = "Stuck User", Email = $"stuck_{Guid.NewGuid():N}@example.com",
                PasswordHash = "x", Phone = "+233500000001", Role = UserRole.Tenant, IsActive = true,
            };
            db.Set<User>().Add(user);
            var ticket = new SupportTicket { UserId = user.Id, Subject = "Cannot verify", Summary = "NIA lookup keeps failing." };
            db.Set<SupportTicket>().Add(ticket);
            await db.SaveChangesAsync();
            ticketId = ticket.Id;
            ticketUserId = user.Id;
        }

        // Register a user, promote to Admin in the DB, then re-login so the JWT carries the
        // Admin role claim (the [Authorize(Roles="Admin")] gate reads the token, not the DB).
        var adminEmail = $"admin_{Guid.NewGuid():N}@example.com";
        var (newAdminId, _) = await RegisterAndLoginAsync(UserRole.Tenant, email: adminEmail);
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Set<User>().FindAsync(newAdminId);
            admin!.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        }
        await LoginAsync(adminEmail);

        var list = await _httpClient.GetAsync("/api/admin/support-tickets");
        var tickets = JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement.GetProperty("data").GetProperty("items");
        Assert.Contains(tickets.EnumerateArray(), t => t.GetProperty("ticketId").GetString() == ticketId);

        var resolve = await _httpClient.PostAsync($"/api/admin/support-tickets/{ticketId}/resolve", null);
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ticket = await db.Set<SupportTicket>().FindAsync(ticketId);
            Assert.Equal(SupportTicketStatus.Resolved, ticket!.Status);
            // The user who raised it was told.
            Assert.True(db.Set<Notification>().Any(n => n.UserId == ticketUserId));
        }
    }

    /// <summary>Logs in an existing account and sets the bearer token (for re-login after a role change).</summary>
    private async Task LoginAsync(string email)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login",
            new TripNest.Core.DTOs.Auth.LoginRequest { Email = email, Password = "Password@123" });
        var token = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }
}
