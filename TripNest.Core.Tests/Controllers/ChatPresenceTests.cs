using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using TripNest.Core.DTOs.Chat;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for the hub's GetPresence method: presence (online / last-seen) is visible to the
/// user themself and their conversation partners only — the same audience that receives the
/// PresenceChanged push — never to arbitrary authenticated users.
/// </summary>
public class ChatPresenceTests : TestBase
{
    private HubConnection BuildConnection(string token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(_fixture.Server.BaseAddress, "hubs/chat"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _fixture.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    /// <summary>
    /// Queries presence until the target shows online. The server registers presence in
    /// OnConnectedAsync, which can still be running when the client's StartAsync returns,
    /// so a single immediate query would be racy.
    /// </summary>
    private static async Task<JsonElement> WaitUntilOnlineAsync(HubConnection caller, string userId)
    {
        JsonElement presence = default;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            presence = await caller.InvokeAsync<JsonElement>("GetPresence", userId);
            if (presence.GetProperty("isOnline").GetBoolean())
                return presence;
            await Task.Delay(100);
        }
        return presence;
    }

    [Fact]
    public async Task GetPresence_ConversationPartner_IsVisible()
    {
        var (idA, tokenA) = await RegisterAndLoginAsync(UserRole.Tenant);
        var (_, tokenB) = await RegisterAndLoginAsync(UserRole.Landlord);

        // The client is authenticated as B (last login); start a conversation with A.
        await _httpClient.PostAsJsonAsync("/api/chat/conversations",
            new StartConversationRequest { OtherUserId = idA });

        await using var connA = BuildConnection(tokenA);
        await using var connB = BuildConnection(tokenB);
        await connA.StartAsync();
        await connB.StartAsync();

        var presence = await WaitUntilOnlineAsync(connB, idA);

        Assert.Equal(idA, presence.GetProperty("userId").GetString());
        Assert.True(presence.GetProperty("isOnline").GetBoolean());
    }

    [Fact]
    public async Task GetPresence_Self_IsAllowed()
    {
        var (idA, tokenA) = await RegisterAndLoginAsync(UserRole.Tenant);

        await using var connA = BuildConnection(tokenA);
        await connA.StartAsync();

        var presence = await WaitUntilOnlineAsync(connA, idA);

        Assert.True(presence.GetProperty("isOnline").GetBoolean());
    }

    [Fact]
    public async Task GetPresence_Stranger_IsDenied()
    {
        var (idA, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        var (_, tokenB) = await RegisterAndLoginAsync(UserRole.Tenant);

        // No conversation exists between A and B, so B must not see A's presence.
        await using var connB = BuildConnection(tokenB);
        await connB.StartAsync();

        var ex = await Assert.ThrowsAsync<HubException>(
            () => connB.InvokeAsync<JsonElement>("GetPresence", idA));
        Assert.Contains("conversation partners", ex.Message);
    }
}
