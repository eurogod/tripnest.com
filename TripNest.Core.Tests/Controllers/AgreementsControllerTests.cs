using System.Net;

namespace TripNest.Core.Tests.Controllers;

public class AgreementsControllerTests : TestBase
{
    [Fact]
    public async Task GetAgreements_Unauthorized_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/agreements/mine");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAgreements_WithValidAuth_ShouldReturnOk()
    {
        // Arrange
        await AuthenticateTenant();

        // Act
        var response = await _httpClient.GetAsync("/api/agreements/mine");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task AuthenticateTenant()
    {
        var registerRequest = new DTOs.Auth.RegisterRequest
        {
            FullName = "Agreement Tenant",
            Email = "agreement-tenant@example.com",
            Password = "Password@123",
            ConfirmPassword = "Password@123",
            Phone = "+233501234567",
            Role = Enums.UserRole.Tenant
        };

        await _httpClient.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new DTOs.Auth.LoginRequest
        {
            Email = "agreement-tenant@example.com",
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
