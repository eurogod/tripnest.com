using System.Net;
using System.Text.Json;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

public class ChatControllerTests : TestBase
{
    [Fact]
    public async Task GetConversations_Unauthenticated_ShouldReturnUnauthorized()
    {
        ClearAuth();
        var response = await _httpClient.GetAsync("/api/Chat/conversations/mine");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetConversations_Authenticated_ShouldReturnOk()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        var response = await _httpClient.GetAsync("/api/Chat/conversations/mine");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StartConversation_SendMessage_AndReadHistory_ShouldWork()
    {
        // Register the recipient first, capture their id.
        var (otherUserId, _) = await RegisterAndLoginAsync(UserRole.Tenant);
        // Register the sender (this resets the auth header to the sender).
        await RegisterAndLoginAsync(UserRole.Tenant);

        // Start a conversation with the recipient.
        var convResp = await _httpClient.PostAsJsonAsync("/api/Chat/conversations", new { otherUserId });
        Assert.Equal(HttpStatusCode.Created, convResp.StatusCode);
        var conversationId = JsonDocument.Parse(await convResp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("conversationId").GetString();

        // Send a message.
        var sendResp = await _httpClient.PostAsJsonAsync(
            $"/api/Chat/conversations/{conversationId}/messages", new { body = "Hello there" });
        Assert.Equal(HttpStatusCode.Created, sendResp.StatusCode);

        // Read history back.
        var histResp = await _httpClient.GetAsync($"/api/Chat/conversations/{conversationId}/messages");
        Assert.Equal(HttpStatusCode.OK, histResp.StatusCode);
        var body = await histResp.Content.ReadAsStringAsync();
        Assert.Contains("Hello there", body);
    }
}
