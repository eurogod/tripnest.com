using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Models;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for the caretaker self-service surface reimplemented from PR #24 onto current main:
/// the real dashboard (was hardcoded zeros) and the self-availability toggle (Suspended rejected).
/// </summary>
public class CaretakerDashboardTests : TestBase
{
    [Fact]
    public async Task CaretakerDashboard_AggregatesRealMetrics()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Caretaker);

        string caretakerId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var caretaker = new Caretaker
            {
                UserId = userId, Status = CaretakerStatus.Active,
                MonthlyCompensation = 500m, ServiceArea = "Accra", Responsibilities = "General upkeep"
            };
            db.Caretakers.Add(caretaker);
            caretakerId = caretaker.Id;

            db.ServiceRequests.AddRange(
                new ServiceRequest { CaretakerId = caretakerId, RequestedByUserId = "someone", PropertyId = "p1",
                    ServiceType = "Repair", Description = "Fix", Status = ServiceRequestStatus.Completed,
                    Rating = 5, CompletedAt = DateTime.UtcNow },
                new ServiceRequest { CaretakerId = caretakerId, RequestedByUserId = "someone", PropertyId = "p1",
                    ServiceType = "Clean", Description = "Tidy", Status = ServiceRequestStatus.Pending });
            await db.SaveChangesAsync();
        }

        var data = await DataOf(await _httpClient.GetAsync("/api/personaldashboard/caretaker"));
        Assert.Equal(2, data.GetProperty("totalServiceRequests").GetInt32());
        Assert.Equal(1, data.GetProperty("completedServiceRequests").GetInt32());
        Assert.Equal(1, data.GetProperty("pendingRequests").GetInt32());
        Assert.Equal(5m, data.GetProperty("averageRating").GetDecimal());
        Assert.Equal(1, data.GetProperty("totalReviews").GetInt32());
        Assert.Equal(500m, data.GetProperty("monthlyCompensation").GetDecimal());
        Assert.Equal(1, data.GetProperty("activeEngagements").GetInt32());
        Assert.Equal(2, data.GetProperty("recentRequests").GetArrayLength());
    }

    [Fact]
    public async Task Availability_SelfToggle_RejectsSuspended()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Caretaker);
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Caretakers.Add(new Caretaker { UserId = userId, Status = CaretakerStatus.Active, ServiceArea = "Accra", Responsibilities = "General upkeep" });
            await db.SaveChangesAsync();
        }

        // Self-suspend is rejected.
        var suspend = await _httpClient.PatchAsJsonAsync("/api/caretakers/me/availability", new { status = (int)CaretakerStatus.Suspended });
        Assert.Equal(HttpStatusCode.BadRequest, suspend.StatusCode);

        // Active → Inactive works.
        var inactive = await _httpClient.PatchAsJsonAsync("/api/caretakers/me/availability", new { status = (int)CaretakerStatus.Inactive });
        Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(CaretakerStatus.Inactive, (await db.Caretakers.FirstAsync(c => c.UserId == userId)).Status);
        }
    }

    private static async Task<JsonElement> DataOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data");
    }
}
