using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using TripNest.Core.DTOs.Auth;
using TripNest.Core.DTOs.Properties;

namespace TripNest.Core.Tests.Controllers;

public class PropertiesControllerTests : TestBase
{
    private string? _landlordToken;
    private string? _landlordId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await AuthenticateAsLandlord();
    }

    private async Task AuthenticateAsLandlord()
    {
        var registerRequest = new RegisterRequest
        {
            FullName = "Landlord User",
            Email = "landlord@example.com",
            Password = "Password@123",
            ConfirmPassword = "Password@123",
            Phone = "+233501234567",
            Role = Enums.UserRole.Landlord
        };

        await _httpClient.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest
        {
            Email = "landlord@example.com",
            Password = "Password@123"
        };

        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginRequest);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        _landlordToken = root.GetProperty("data").GetProperty("accessToken").GetString();
        _landlordId = root.GetProperty("data").GetProperty("userId").GetString();

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _landlordToken);
    }

    [Fact]
    public async Task CreateProperty_WithValidData_ShouldReturnCreated()
    {
        // Arrange
        var createRequest = new CreatePropertyRequest
        {
            Title = "Test Property",
            Description = "A test property listing",
            Location = "Tarkwa, Ghana",
            Latitude = 5.2802,
            Longitude = -1.5857,
            Bedrooms = 2,
            Bathrooms = 1,
            MonthlyRent = 2000m,
            DailyRate = 100m,
            PropertyType = "Apartment",
            Amenities = "WiFi,TV,AirConditioning"
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/properties", createRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GetAllProperties_ShouldReturnOk()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/properties");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateProperty_Unauthorized_ShouldReturnUnauthorized()
    {
        // Arrange
        _httpClient.DefaultRequestHeaders.Authorization = null;

        var createRequest = new CreatePropertyRequest
        {
            Title = "Test Property",
            Description = "A test property listing",
            Location = "Tarkwa, Ghana",
            Latitude = 5.2802,
            Longitude = -1.5857,
            Bedrooms = 2,
            Bathrooms = 1,
            MonthlyRent = 2000m,
            DailyRate = 100m,
            PropertyType = "Apartment",
            Amenities = "WiFi,TV"
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/properties", createRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
