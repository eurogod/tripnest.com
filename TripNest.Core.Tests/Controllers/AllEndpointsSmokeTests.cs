using System.Net;
using System.Text;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Contract smoke coverage for every HTTP endpoint in the API. Rather than build full
/// happy-path fixtures for each money/booking flow, this asserts the security contract
/// of each route:
///   • protected routes reject anonymous callers with 401,
///   • public routes do not require authentication,
///   • role-restricted and verification-gated routes return 403 for the wrong caller.
/// This guards against accidentally dropping an [Authorize]/[RequireVerified] attribute or
/// removing/renaming a route.
/// </summary>
public class AllEndpointsSmokeTests : TestBase
{
    private static HttpRequestMessage Build(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT" or "PATCH")
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        return request;
    }

    // Every endpoint that requires authentication (incl. role-restricted ones — those still
    // return 401 before the role check when no token is present). Placeholder ids are fine:
    // authorization runs before model binding / entity lookup.
    [Theory]
    // Auth
    [InlineData("GET", "/api/auth/me")]
    [InlineData("POST", "/api/auth/change-password")]
    [InlineData("POST", "/api/auth/phone/send-otp")]
    [InlineData("POST", "/api/auth/phone/verify-otp")]
    // Verification
    [InlineData("POST", "/api/verification/start")]
    [InlineData("GET", "/api/verification/status")]
    // Properties (+ availability + walkthroughs share the route prefix)
    [InlineData("POST", "/api/properties")]
    [InlineData("PUT", "/api/properties/x")]
    [InlineData("DELETE", "/api/properties/x")]
    [InlineData("GET", "/api/properties/user/my-properties")]
    [InlineData("POST", "/api/properties/x/blocked-dates")]
    [InlineData("DELETE", "/api/properties/x/blocked-dates/y")]
    // (walkthrough UPLOAD is multipart-only — covered separately below)
    [InlineData("PATCH", "/api/properties/x/walkthrough/review")]
    [InlineData("GET", "/api/properties/pending-walkthroughs")]
    [InlineData("DELETE", "/api/properties/x/walkthroughs/y")]
    // Bookings
    [InlineData("POST", "/api/bookings")]
    [InlineData("GET", "/api/bookings/x")]
    [InlineData("GET", "/api/bookings/user/my-bookings")]
    [InlineData("POST", "/api/bookings/x/cancel")]
    [InlineData("GET", "/api/bookings/x/cancellation-preview")]
    // Escrow
    [InlineData("POST", "/api/escrow/initiate")]
    [InlineData("GET", "/api/escrow/x")]
    [InlineData("POST", "/api/escrow/x/release")]
    [InlineData("POST", "/api/escrow/x/dispute")]
    [InlineData("PATCH", "/api/escrow/x/resolve-dispute")]
    [InlineData("POST", "/api/escrow/x/refund")]
    // Agreements
    [InlineData("POST", "/api/agreements")]
    [InlineData("GET", "/api/agreements/mine")]
    [InlineData("GET", "/api/agreements/x")]
    [InlineData("POST", "/api/agreements/x/sign")]
    [InlineData("GET", "/api/agreements/x/download")]
    // Chat
    [InlineData("GET", "/api/chat/conversations/mine")]
    [InlineData("POST", "/api/chat/conversations")]
    [InlineData("GET", "/api/chat/conversations/x")]
    [InlineData("GET", "/api/chat/conversations/x/messages")]
    [InlineData("POST", "/api/chat/conversations/x/messages")]
    [InlineData("PATCH", "/api/chat/messages/x/read")]
    [InlineData("PATCH", "/api/chat/conversations/x/mark-read")]
    [InlineData("DELETE", "/api/chat/conversations/x")]
    // Caretakers
    [InlineData("POST", "/api/caretakers/assign")]
    [InlineData("POST", "/api/caretakers/service-requests")]
    [InlineData("GET", "/api/caretakers/service-requests/mine")]
    [InlineData("PATCH", "/api/caretakers/service-requests/x/accept")]
    [InlineData("PATCH", "/api/caretakers/service-requests/x/status")]
    [InlineData("POST", "/api/caretakers/service-requests/x/review")]
    // Agents
    [InlineData("POST", "/api/agents/x/viewing-requests")]
    [InlineData("PATCH", "/api/agents/viewing-requests/x/status")]
    // Maintenance
    [InlineData("POST", "/api/maintenance")]
    [InlineData("GET", "/api/maintenance/property/x")]
    [InlineData("GET", "/api/maintenance/mine")]
    [InlineData("PATCH", "/api/maintenance/x/status")]
    [InlineData("POST", "/api/maintenance/x/convert-to-service-request")]
    // Reviews
    [InlineData("POST", "/api/reviews")]
    [InlineData("GET", "/api/reviews/mine")]
    [InlineData("DELETE", "/api/reviews/x")]
    // Notifications
    [InlineData("GET", "/api/notifications/mine")]
    [InlineData("PATCH", "/api/notifications/x/read")]
    [InlineData("PATCH", "/api/notifications/mark-all-read")]
    [InlineData("GET", "/api/notifications/unread-count")]
    [InlineData("DELETE", "/api/notifications/x")]
    // Receipts
    [InlineData("GET", "/api/receipts/mine")]
    [InlineData("GET", "/api/receipts/x")]
    [InlineData("GET", "/api/receipts/x/download")]
    [InlineData("GET", "/api/receipts/booking/x")]
    // Wishlist
    [InlineData("GET", "/api/wishlist/mine")]
    [InlineData("POST", "/api/wishlist/x")]
    [InlineData("DELETE", "/api/wishlist/x")]
    // Profile
    [InlineData("GET", "/api/profile/me")]
    [InlineData("PUT", "/api/profile/me")]
    // Settings
    [InlineData("PUT", "/api/settings/password")]
    [InlineData("DELETE", "/api/settings/account")]
    // Safety
    [InlineData("POST", "/api/safety/checkin")]
    [InlineData("POST", "/api/safety/alert")]
    // Trust score
    [InlineData("POST", "/api/trustscore/stay-feedback")]
    // Dashboards
    [InlineData("GET", "/api/personaldashboard/tenant")]
    [InlineData("GET", "/api/personaldashboard/landlord")]
    [InlineData("GET", "/api/personaldashboard/agent")]
    [InlineData("GET", "/api/personaldashboard/caretaker")]
    [InlineData("GET", "/api/landlord/stats")]
    [InlineData("GET", "/api/landlord/earnings")]
    [InlineData("GET", "/api/landlord/properties/performance")]
    [InlineData("GET", "/api/admin/stats")]
    [InlineData("GET", "/api/admin/audit-logs")]
    public async Task ProtectedEndpoint_Anonymous_ShouldReturn401(string method, string path)
    {
        ClearAuth();
        var response = await _httpClient.SendAsync(Build(method, path));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Endpoints that must be reachable without authentication. We assert they are not blocked
    // by auth (not 401/403); the concrete 2xx/400/404 body depends on data and isn't the contract.
    [Theory]
    [InlineData("POST", "/api/auth/register")]
    [InlineData("POST", "/api/auth/login")]
    [InlineData("POST", "/api/auth/refresh-token")]
    [InlineData("POST", "/api/auth/forgot-password")]
    [InlineData("POST", "/api/auth/reset-password")]
    [InlineData("GET", "/api/properties")]
    [InlineData("GET", "/api/properties/x")]
    [InlineData("GET", "/api/properties/search?location=Accra")]
    [InlineData("GET", "/api/properties/x/availability")]
    [InlineData("GET", "/api/properties/x/available-ranges")]
    [InlineData("GET", "/api/properties/x/walkthroughs")]
    [InlineData("GET", "/api/properties/x/walkthroughs/y")]
    // (escrow webhook is anonymous but HMAC-guarded — covered separately below)
    [InlineData("GET", "/api/caretakers")]
    [InlineData("GET", "/api/caretakers/x")]
    [InlineData("GET", "/api/agents")]
    [InlineData("GET", "/api/agents/x")]
    [InlineData("GET", "/api/reviews/property/x")]
    [InlineData("GET", "/api/reviews/x")]
    [InlineData("GET", "/api/trustscore/property/x")]
    [InlineData("GET", "/api/trustscore/user/x")]
    [InlineData("GET", "/api/search")]
    [InlineData("GET", "/api/config/app-info")]
    public async Task PublicEndpoint_Anonymous_ShouldNotRequireAuth(string method, string path)
    {
        ClearAuth();
        var response = await _httpClient.SendAsync(Build(method, path));
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WalkthroughUpload_Anonymous_ShouldReturn401()
    {
        // Multipart so the [Consumes("multipart/form-data")] constraint matches and the
        // [Authorize] check is reached.
        ClearAuth();
        using var content = new MultipartFormDataContent
        {
            { new StringContent("Tour"), "title" },
            { new ByteArrayContent(new byte[] { 1, 2, 3 }), "videoFile", "tour.mp4" }
        };
        var response = await _httpClient.PostAsync("/api/properties/x/walkthrough", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EscrowWebhook_WithoutValidSignature_ShouldBeRejected()
    {
        // The webhook is [AllowAnonymous] but verifies an HMAC signature; an unsigned call
        // must NOT be processed (no money moved). Missing signature -> 401.
        ClearAuth();
        var response = await _httpClient.PostAsync("/api/escrow/webhook",
            new StringContent("{\"bookingId\":\"x\",\"reference\":\"y\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RoleRestrictedEndpoint_WrongRole_ShouldReturnForbidden()
    {
        // A tenant hitting an Admin-only route must be forbidden (authenticated, wrong role).
        await RegisterAndLoginAsync(UserRole.Tenant);
        var response = await _httpClient.GetAsync("/api/admin/stats");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task VerificationGate_UnverifiedLandlord_ShouldReturnForbidden()
    {
        await RegisterAndLoginAsync(UserRole.Landlord);
        var response = await _httpClient.PostAsync("/api/properties",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task VerificationGate_VerifiedLandlord_ShouldNotBeForbidden()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(userId);

        // With identity verified the gate lets the request through (it may then 400 on the
        // empty body, but it must NOT be 403/401).
        var response = await _httpClient.PostAsync("/api/properties",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
