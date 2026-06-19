using System.Net;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Smoke/authorization coverage across the remaining modules: each verifies the
/// route exists, enforces auth, and returns sane status codes against a fresh DB.
/// </summary>
public class ReviewsControllerTests : TestBase
{
    [Fact]
    public async Task GetMyReviews_Unauthenticated_ShouldReturnUnauthorized()
    {
        ClearAuth();
        var response = await _httpClient.GetAsync("/api/Reviews/mine");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyReviews_Authenticated_ShouldReturnOk()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        var response = await _httpClient.GetAsync("/api/Reviews/mine");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public class NotificationsControllerTests : TestBase
{
    [Fact]
    public async Task GetMine_Unauthenticated_ShouldReturnUnauthorized()
    {
        ClearAuth();
        var response = await _httpClient.GetAsync("/api/Notifications/mine");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMineAndUnreadCount_Authenticated_ShouldReturnOk()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);

        var mine = await _httpClient.GetAsync("/api/Notifications/mine");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);

        var count = await _httpClient.GetAsync("/api/Notifications/unread-count");
        Assert.Equal(HttpStatusCode.OK, count.StatusCode);
    }
}

public class ProfileControllerTests : TestBase
{
    [Fact]
    public async Task GetMe_Unauthenticated_ShouldReturnUnauthorized()
    {
        ClearAuth();
        var response = await _httpClient.GetAsync("/api/Profile/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_Authenticated_ShouldReturnOk()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        var response = await _httpClient.GetAsync("/api/Profile/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public class MaintenanceControllerTests : TestBase
{
    [Fact]
    public async Task GetMine_Unauthenticated_ShouldReturnUnauthorized()
    {
        ClearAuth();
        var response = await _httpClient.GetAsync("/api/Maintenance/mine");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMine_AsTenant_ShouldReturnOk()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        var response = await _httpClient.GetAsync("/api/Maintenance/mine");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetPropertyMaintenance_ForMissingProperty_ShouldReturnNotFound()
    {
        // Regression test: previously threw 500 for a non-existent property; should be 404.
        await RegisterAndLoginAsync(UserRole.Landlord);
        var response = await _httpClient.GetAsync(
            $"/api/Maintenance/property/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public class VerificationControllerTests : TestBase
{
    [Fact]
    public async Task Start_Unauthenticated_ShouldReturnUnauthorized()
    {
        ClearAuth();
        var response = await _httpClient.PostAsJsonAsync("/api/Verification/start",
            new { ghanaCardNumber = "GHA-0000-000000", selfiePhotoPath = "/tmp/x.jpg", firstName = "A", lastName = "B", dateOfBirth = "1990-01-01" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Status_WithNoVerification_ShouldReturnClientError()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        var response = await _httpClient.GetAsync("/api/Verification/status");
        // No verification exists yet -> a 4xx (the service reports "not found").
        Assert.True((int)response.StatusCode >= 400 && (int)response.StatusCode < 500,
            $"expected a 4xx, got {(int)response.StatusCode}");
    }
}

public class PublicEndpointsTests : TestBase
{
    [Fact]
    public async Task HealthCheck_ShouldReturnOk()
    {
        var response = await _httpClient.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ConfigAppInfo_ShouldReturnOk_WithoutAuth()
    {
        var response = await _httpClient.GetAsync("/api/Config/app-info");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ListProperties_ShouldReturnOk_WithoutAuth()
    {
        var response = await _httpClient.GetAsync("/api/properties");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
