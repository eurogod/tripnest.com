using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using TripNest.Core.DTOs.Chat;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// End-to-end coverage for the real-time SignalR chat hub (/hubs/chat). Connects two live
/// HubConnection clients through the in-memory test server (no Azure / running app needed)
/// and asserts that a message and a typing indicator are delivered to the other participant
/// in real time. Uses the LongPolling transport so the SignalR client routes through the
/// TestServer's HttpMessageHandler, and JWTs flow via the Authorization header.
/// </summary>
public class ChatHubRealtimeTests : TestBase
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(10);

    private HubConnection BuildConnection(string token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(_fixture.Server.BaseAddress, "hubs/chat"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _fixture.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    private async Task<(string IdA, string TokenA, string IdB, string TokenB, string ConversationId)> SetupConversationAsync()
    {
        var (idA, tokenA) = await RegisterAndLoginAsync(UserRole.Tenant);
        var (idB, tokenB) = await RegisterAndLoginAsync(UserRole.Landlord);

        // RegisterAndLoginAsync leaves the client authenticated as the last user (B); start
        // the conversation as B with A as the other participant.
        var response = await _httpClient.PostAsJsonAsync("/api/chat/conversations",
            new StartConversationRequest { OtherUserId = idA });
        var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data");
        var conversationId = data.GetProperty("conversationId").GetString()!;

        return (idA, tokenA, idB, tokenB, conversationId);
    }

    private static async Task<T> AwaitOrTimeout<T>(TaskCompletionSource<T> tcs, string what)
    {
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(ReceiveTimeout));
        Assert.True(completed == tcs.Task, $"Timed out waiting for {what}");
        return await tcs.Task;
    }

    [Fact]
    public async Task SendMessage_ShouldBroadcastToOtherParticipantLive()
    {
        var (idA, tokenA, _, tokenB, conversationId) = await SetupConversationAsync();

        await using var connA = BuildConnection(tokenA);
        await using var connB = BuildConnection(tokenB);

        var received = new TaskCompletionSource<MessageResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        connB.On<MessageResponse>("ReceiveMessage", msg => received.TrySetResult(msg));

        await connA.StartAsync();
        await connB.StartAsync();
        await connA.InvokeAsync("JoinConversation", conversationId);
        await connB.InvokeAsync("JoinConversation", conversationId);

        await connA.InvokeAsync("SendMessage", conversationId, "hello from A");

        var message = await AwaitOrTimeout(received, "the real-time message");
        Assert.Equal("hello from A", message.Content);
        Assert.Equal(idA, message.SenderId);
        Assert.Equal(conversationId, message.ConversationId);
    }

    [Fact]
    public async Task Typing_ShouldNotifyOtherParticipantLive()
    {
        var (idA, tokenA, _, tokenB, conversationId) = await SetupConversationAsync();

        await using var connA = BuildConnection(tokenA);
        await using var connB = BuildConnection(tokenB);

        var typingUser = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        connB.On<JsonElement>("UserTyping", payload =>
            typingUser.TrySetResult(payload.GetProperty("userId").GetString()!));

        await connA.StartAsync();
        await connB.StartAsync();
        await connA.InvokeAsync("JoinConversation", conversationId);
        await connB.InvokeAsync("JoinConversation", conversationId);

        await connA.InvokeAsync("Typing", conversationId);

        // The hub broadcasts UserTyping to OthersInGroup only, so B (not A) receives it.
        var notifiedUserId = await AwaitOrTimeout(typingUser, "the typing indicator");
        Assert.Equal(idA, notifiedUserId);
    }
}
