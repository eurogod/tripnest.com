using System.Net;
using TripNest.Core.DTOs.Auth;

namespace TripNest.Core.Tests.Controllers;

public class BookingsControllerTests : TestBase
{
    [Fact]
    public async Task GetBookings_Unauthorized_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/bookings/user/my-bookings");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBookings_WithValidAuth_ShouldReturnOk()
    {
        // Arrange
        await AuthenticateAsTenant();

        // Act
        var response = await _httpClient.GetAsync("/api/bookings/user/my-bookings");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task AuthenticateAsTenant()
    {
        var registerRequest = new RegisterRequest
        {
            FullName = "Tenant User",
            Email = "tenant@example.com",
            Password = "Password@123",
            ConfirmPassword = "Password@123",
            Phone = "+233501234567",
            Role = Enums.UserRole.Tenant
        };

        await _httpClient.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest
        {
            Email = "tenant@example.com",
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
