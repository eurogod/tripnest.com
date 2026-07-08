using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// AI in chat: reply suggestions (participant-only, edited before sending) and off-platform
/// payment scam detection (warns the recipient, never blocks the message).
/// </summary>
public class ChatAiTests : TestBase
{
    [Fact]
    public async Task SuggestReply_ReturnsDraft_ForParticipant()
    {
        var (otherUserId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await RegisterAndLoginAsync(UserRole.Tenant);
        var conversationId = await StartConversationAndMessageAsync(otherUserId, "Is there parking at the apartment?");

        var stub = _fixture.Services.GetRequiredService<StubAiClient>();
        stub.Configured = true;
        stub.NextCompletion = """{"reply": "Yes — there's dedicated on-site parking included."}""";

        var res = await _httpClient.PostAsync($"/api/chat/conversations/{conversationId}/suggest-reply", null);
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.OK, $"Expected OK but got {res.StatusCode}: {body}");
        var reply = JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("reply").GetString();
        Assert.Contains("parking", reply!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuggestReply_ByNonParticipant_IsForbidden()
    {
        var (otherUserId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await RegisterAndLoginAsync(UserRole.Tenant);
        var conversationId = await StartConversationAndMessageAsync(otherUserId, "Hello");

        // A third party who is not in the conversation.
        await RegisterAndLoginAsync(UserRole.Tenant);
        var res = await _httpClient.PostAsync($"/api/chat/conversations/{conversationId}/suggest-reply", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task OffPlatformPaymentMessage_WarnsRecipient_ButStillDelivers()
    {
        var (recipientId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await RegisterAndLoginAsync(UserRole.Tenant); // sender

        var stub = _fixture.Services.GetRequiredService<StubAiClient>();
        stub.Configured = true;
        stub.NextCompletion = """{"scam": true, "reason": "asks to pay via MoMo directly"}""";

        var conversationId = await StartConversationAsync(recipientId);
        var scammy = "Just send the money to my MTN MoMo directly on 0241234567 and skip the platform fee.";
        var send = await _httpClient.PostAsJsonAsync(
            $"/api/chat/conversations/{conversationId}/messages", new { body = scammy });

        // The message is NOT blocked — delivery succeeds.
        Assert.Equal(HttpStatusCode.Created, send.StatusCode);

        // …and the recipient got a safety warning.
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var warned = db.Set<Notification>().Any(n =>
            n.UserId == recipientId && n.Type == NotificationType.SafetyAlert);
        Assert.True(warned, "recipient should have received an off-platform-payment safety warning");
    }

    [Fact]
    public async Task OrdinaryMessage_DoesNotWarn()
    {
        var (recipientId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        await RegisterAndLoginAsync(UserRole.Tenant);

        var conversationId = await StartConversationAsync(recipientId);
        var send = await _httpClient.PostAsJsonAsync(
            $"/api/chat/conversations/{conversationId}/messages",
            new { body = "Great, looking forward to the stay! What time is check-in?" });
        Assert.Equal(HttpStatusCode.Created, send.StatusCode);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var warned = db.Set<Notification>().Any(n =>
            n.UserId == recipientId && n.Type == NotificationType.SafetyAlert);
        Assert.False(warned, "an ordinary message must not trigger a safety warning");
    }

    private async Task<string> StartConversationAsync(string otherUserId)
    {
        var conv = await _httpClient.PostAsJsonAsync("/api/chat/conversations", new { otherUserId });
        Assert.Equal(HttpStatusCode.Created, conv.StatusCode);
        return JsonDocument.Parse(await conv.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("conversationId").GetString()!;
    }

    private async Task<string> StartConversationAndMessageAsync(string otherUserId, string firstMessage)
    {
        var conversationId = await StartConversationAsync(otherUserId);
        await _httpClient.PostAsJsonAsync(
            $"/api/chat/conversations/{conversationId}/messages", new { body = firstMessage });
        return conversationId;
    }
}
