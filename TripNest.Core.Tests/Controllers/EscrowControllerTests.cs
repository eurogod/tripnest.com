using System.Net;

namespace TripNest.Core.Tests.Controllers;

public class EscrowControllerTests : TestBase
{
    [Fact]
    public async Task GetEscrow_Unauthorized_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/escrow/nonexistent");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEscrow_NotFound_ShouldReturnNotFound()
    {
        // Arrange
        await AuthenticateAsTenant();

        // Act
        var response = await _httpClient.GetAsync("/api/escrow/nonexistent");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task AuthenticateAsTenant()
    {
        var registerRequest = new DTOs.Auth.RegisterRequest
        {
            FullName = "Tenant User",
            Email = "escrow-tenant@example.com",
            Password = "Password@123",
            ConfirmPassword = "Password@123",
            Phone = "+233501234567",
            Role = Enums.UserRole.Tenant
        };

        await _httpClient.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new DTOs.Auth.LoginRequest
        {
            Email = "escrow-tenant@example.com",
            Password = "Password@123"
        };

        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginRequest);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        var token = root.GetProperty("data").GetProperty("accessToken").GetString();
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }
}
