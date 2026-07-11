using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Auth;
using TripNest.Core.DTOs.Marketplace;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// End-to-end coverage for the marketplace / operations modules (pricing, calendar, tasks, team,
/// exchange, resources, statements, payment methods, tour, inquiries, landlord workspace, featured).
/// </summary>
public class MarketplaceEndpointsTests : TestBase
{
    private void UseToken(string token) =>
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    /// <summary>
    /// Admin can't be self-registered via the public endpoint, so register a normal user, elevate
    /// their role in the DB, then re-login to obtain a token that actually carries the Admin role claim.
    /// </summary>
    private async Task LoginAsNewAdminAsync()
    {
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await RegisterAndLoginAsync(UserRole.Tenant, email);

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        }

        var res = await _httpClient.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = "Password@123" });
        var data = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        UseToken(data.GetProperty("accessToken").GetString()!);
    }

    private async Task<(string PropertyId, string OwnerToken)> CreatePropertyAsync()
    {
        var (landlordId, ownerToken) = await RegisterAndLoginAsync(UserRole.Landlord);
        await MarkUserVerifiedAsync(landlordId);

        var create = new CreatePropertyRequest
        {
            Title = "2 Bedroom Apartment",
            Description = "Spacious and bright",
            Location = "Accra, Ghana",
            Latitude = 5.6037,
            Longitude = -0.1870,
            Bedrooms = 2,
            Bathrooms = 2,
            MonthlyRent = 3000m,
            DailyRate = 120m,
            PropertyType = "Apartment",
            StayType = StayType.ShortTerm,
            CancellationPolicy = CancellationPolicy.Moderate
        };
        var res = await _httpClient.PostAsJsonAsync("/api/properties", create);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var data = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        return (data.GetProperty("propertyId").GetString()!, ownerToken);
    }

    private static async Task<JsonElement> DataOf(HttpResponseMessage res)
        => JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("data");

    // ----------------------------------------------------------------- Pricing
    [Fact]
    public async Task Pricing_DefaultsThenUpdates_RoundTrips()
    {
        var (propertyId, ownerToken) = await CreatePropertyAsync();

        // Defaults are derived from the listing when nothing is saved.
        var getRes = await _httpClient.GetAsync($"/api/pricing/{propertyId}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var defaults = await DataOf(getRes);
        Assert.Equal(120m, defaults.GetProperty("baseRate").GetDecimal());

        var update = new UpdatePricingSettingsRequest
        {
            BaseRate = 150m,
            WeekendRate = 200m,
            WeeklyDiscountPercent = 10m,
            MonthlyDiscountPercent = 20m,
            MinNights = 2,
            CleaningFee = 50m
        };
        var putRes = await _httpClient.PutAsJsonAsync($"/api/pricing/{propertyId}", update);
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);

        var saved = await DataOf(await _httpClient.GetAsync($"/api/pricing/{propertyId}"));
        Assert.Equal(150m, saved.GetProperty("baseRate").GetDecimal());
        Assert.Equal(2, saved.GetProperty("minNights").GetInt32());
    }

    [Fact]
    public async Task Pricing_OtherLandlord_IsForbidden()
    {
        var (propertyId, _) = await CreatePropertyAsync();

        // A different landlord must not read/modify the owner's pricing.
        await RegisterAndLoginAsync(UserRole.Landlord);
        var res = await _httpClient.GetAsync($"/api/pricing/{propertyId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ---------------------------------------------------------------- Calendar
    [Fact]
    public async Task Calendar_ReturnsOneEntryPerDay()
    {
        var (propertyId, ownerToken) = await CreatePropertyAsync();
        var res = await _httpClient.GetAsync($"/api/calendar?propertyId={propertyId}&year=2026&month=2");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var data = await DataOf(res);
        Assert.Equal(28, data.GetProperty("days").GetArrayLength()); // Feb 2026
    }

    // ------------------------------------------------------------- Host tasks
    [Fact]
    public async Task HostTasks_Crud_Works()
    {
        await RegisterAndLoginAsync(UserRole.Landlord);

        var create = new CreateHostTaskRequest { Title = "Deep clean", Type = "Cleaning", Priority = "High" };
        var createRes = await _httpClient.PostAsJsonAsync("/api/tasks", create);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var id = (await DataOf(createRes)).GetProperty("id").GetString()!;

        var listRes = await _httpClient.GetAsync("/api/tasks");
        var page = await DataOf(listRes);
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());

        var patch = await _httpClient.PatchAsJsonAsync($"/api/tasks/{id}", new UpdateHostTaskRequest { Status = "Done" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal((int)HostTaskStatus.Done, (await DataOf(patch)).GetProperty("status").GetInt32());

        var del = await _httpClient.DeleteAsync($"/api/tasks/{id}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
    }

    [Fact]
    public async Task HostTasks_InvalidType_IsBadRequest()
    {
        await RegisterAndLoginAsync(UserRole.Landlord);
        var res = await _httpClient.PostAsJsonAsync("/api/tasks",
            new CreateHostTaskRequest { Title = "x", Type = "Nonsense", Priority = "Low" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ------------------------------------------------------------------- Team
    [Fact]
    public async Task Team_InviteThenList()
    {
        await RegisterAndLoginAsync(UserRole.Landlord);
        var invite = new InviteTeamMemberRequest { Name = "Ama", Email = "ama@example.com", Role = "Cleaner" };
        var inviteRes = await _httpClient.PostAsJsonAsync("/api/team", invite);
        Assert.Equal(HttpStatusCode.Created, inviteRes.StatusCode);
        Assert.Equal((int)TeamMemberStatus.Invited, (await DataOf(inviteRes)).GetProperty("status").GetInt32());

        var list = await DataOf(await _httpClient.GetAsync("/api/team"));
        Assert.Equal(1, list.GetArrayLength());
    }

    // --------------------------------------------------------------- Exchange
    [Fact]
    public async Task Exchange_PostAndReply()
    {
        await RegisterAndLoginAsync(UserRole.Landlord);

        var postRes = await _httpClient.PostAsJsonAsync("/api/exchange/posts",
            new CreateExchangePostRequest { Title = "Best plumbers?", Body = "Recommendations?", Category = "Suppliers" });
        Assert.Equal(HttpStatusCode.Created, postRes.StatusCode);
        var postId = (await DataOf(postRes)).GetProperty("id").GetString()!;

        await _httpClient.PostAsJsonAsync($"/api/exchange/posts/{postId}/replies",
            new CreateExchangeReplyRequest { Body = "Try Kofi at +233..." });

        var posts = await DataOf(await _httpClient.GetAsync("/api/exchange/posts"));
        Assert.Equal(1, posts.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, posts.GetProperty("items")[0].GetProperty("replyCount").GetInt32());

        var replies = await DataOf(await _httpClient.GetAsync($"/api/exchange/posts/{postId}/replies"));
        Assert.Equal(1, replies.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, replies.GetProperty("items").GetArrayLength());
    }

    // -------------------------------------------------------------- Resources
    [Fact]
    public async Task Resources_AdminCanCreate_LandlordCannot()
    {
        await RegisterAndLoginAsync(UserRole.Landlord);
        var forbidden = await _httpClient.PostAsJsonAsync("/api/resources",
            new CreateResourceRequest { Title = "Guide", Description = "d", Category = "Guide", Format = "PDF", Url = "https://x/y.pdf" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await LoginAsNewAdminAsync();
        var created = await _httpClient.PostAsJsonAsync("/api/resources",
            new CreateResourceRequest { Title = "Guide", Description = "d", Category = "Guide", Format = "PDF", Url = "https://x/y.pdf" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var list = await DataOf(await _httpClient.GetAsync("/api/resources"));
        Assert.Equal(1, list.GetProperty("items").GetArrayLength());
    }

    // ------------------------------------------------------- Payment methods
    [Fact]
    public async Task PaymentMethods_FirstIsPrimary_SecondPromotable()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);

        var first = await DataOf(await _httpClient.PostAsJsonAsync("/api/payments/methods",
            new CreatePaymentMethodRequest { Provider = "MTN MoMo", MaskedNumber = "**** 1234", Channel = "momo" }));
        Assert.True(first.GetProperty("isPrimary").GetBoolean());

        var second = await DataOf(await _httpClient.PostAsJsonAsync("/api/payments/methods",
            new CreatePaymentMethodRequest { Provider = "Visa", MaskedNumber = "**** 4242", Channel = "card" }));
        Assert.False(second.GetProperty("isPrimary").GetBoolean());

        var secondId = second.GetProperty("id").GetString()!;
        var promote = await _httpClient.PatchAsync($"/api/payments/methods/{secondId}/primary", null);
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);

        var list = await DataOf(await _httpClient.GetAsync("/api/payments/methods"));
        var primary = list.EnumerateArray().Single(m => m.GetProperty("isPrimary").GetBoolean());
        Assert.Equal(secondId, primary.GetProperty("id").GetString());
    }

    // -------------------------------------------------------------- Tour
    [Fact]
    public async Task Tour_UpsertThenGetPublicly()
    {
        var (propertyId, ownerToken) = await CreatePropertyAsync();

        var upsert = new UpsertPropertyTourRequest
        {
            Title = "Walkthrough",
            Rooms = new List<TourRoomDto>
            {
                new() { Id = "r1", Name = "Living room", Hotspots = new List<TourHotspotDto>
                    { new() { Id = "h1", X = 50, Y = 50, Label = "Sofa" } } }
            }
        };
        var put = await _httpClient.PutAsJsonAsync($"/api/properties/{propertyId}/tour", upsert);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        ClearAuth(); // tour GET is public
        var get = await _httpClient.GetAsync($"/api/properties/{propertyId}/tour");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var data = await DataOf(get);
        Assert.Equal("Living room", data.GetProperty("rooms")[0].GetProperty("name").GetString());
    }

    // ----------------------------------------------------------- Statements
    [Fact]
    public async Task Statements_EmptyForNewLandlord()
    {
        await RegisterAndLoginAsync(UserRole.Landlord);
        var data = await DataOf(await _httpClient.GetAsync("/api/statements"));
        Assert.Equal(0, data.GetProperty("items").GetArrayLength());
    }

    // ------------------------------------------------ Landlord workspace + inquiry
    [Fact]
    public async Task Inquiry_CreatedByGuest_AppearsForLandlord()
    {
        var (propertyId, ownerToken) = await CreatePropertyAsync();

        // A tenant sends an enquiry.
        await RegisterAndLoginAsync(UserRole.Tenant);
        var created = await _httpClient.PostAsJsonAsync("/api/inquiries",
            new CreateInquiryRequest { PropertyId = propertyId, GuestName = "Yaa", Message = "Is it available in March?" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // The owner sees it in their workspace.
        UseToken(ownerToken);
        var data = await DataOf(await _httpClient.GetAsync("/api/landlord/inquiries"));
        Assert.Equal(1, data.GetProperty("totalCount").GetInt32());
        Assert.Equal("Yaa", data.GetProperty("items")[0].GetProperty("guestName").GetString());
    }

    [Fact]
    public async Task LandlordWorkspace_EmptyListsArePaged()
    {
        await RegisterAndLoginAsync(UserRole.Landlord);

        var bookings = await DataOf(await _httpClient.GetAsync("/api/landlord/bookings"));
        Assert.Equal(0, bookings.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, bookings.GetProperty("page").GetInt32());

        var tenants = await DataOf(await _httpClient.GetAsync("/api/landlord/tenants"));
        Assert.Equal(0, tenants.GetProperty("totalCount").GetInt32());
    }

    // -------------------------------------------------------------- Featured
    [Fact]
    public async Task Featured_IsPublic()
    {
        ClearAuth();
        var res = await _httpClient.GetAsync("/api/properties/featured?limit=4");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
