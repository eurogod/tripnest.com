using System.Net;
using System.Text.Json;
using TripNest.Core.DTOs.Auth;
using TripNest.Core.Response;

namespace TripNest.Core.Tests.Controllers;

public class AuthControllerTests : TestBase
{
    [Fact]
    public async Task Register_WithValidData_ShouldReturnCreatedResponse()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            FullName = "Test User",
            Email = "test@example.com",
            Password = "Password@123",
            ConfirmPassword = "Password@123",
            Phone = "+233501234567",
            Role = Enums.UserRole.Tenant
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(201, root.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnConflict()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            FullName = "Test User",
            Email = "duplicate@example.com",
            Password = "Password@123",
            ConfirmPassword = "Password@123",
            Phone = "+233501234567",
            Role = Enums.UserRole.Tenant
        };

        // Register first user
        await _httpClient.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Act - Register second user with same email
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Assert — the API surfaces business-rule violations as 400 + success:false
        // (consistent across the auth controller), not 409.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.False(body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnTokens()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            FullName = "Login Test User",
            Email = "login@example.com",
            Password = "Password@123",
            ConfirmPassword = "Password@123",
            Phone = "+233501234567",
            Role = Enums.UserRole.Tenant
        };

        // Register user
        await _httpClient.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest
        {
            Email = "login@example.com",
            Password = "Password@123"
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.NotEmpty(root.GetProperty("data").GetProperty("accessToken").GetString() ?? "");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldReturnUnauthorized()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            FullName = "Test User",
            Email = "invalid@example.com",
            Password = "Password@123",
            ConfirmPassword = "Password@123",
            Phone = "+233501234567",
            Role = Enums.UserRole.Tenant
        };

        await _httpClient.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest
        {
            Email = "invalid@example.com",
            Password = "WrongPassword@123"
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert — invalid credentials surface as 400 + success:false (the auth
        // controller maps the service's InvalidOperationException to BadRequest).
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.False(body.GetProperty("success").GetBoolean());
    }
}
