using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Communications module: verifies the SMS/email opt-out is honoured for normal notifications
/// and — critically — that emergency safety alerts bypass the opt-out and are flagged as an
/// override. Uses recording sender doubles registered in <see cref="TestFixture"/>.
/// </summary>
public class CommunicationsTests : TestBase
{
    private RecordingSmsSender Sms => _fixture.Services.GetRequiredService<RecordingSmsSender>();
    private RecordingEmailSender Email => _fixture.Services.GetRequiredService<RecordingEmailSender>();
    [Fact]
    public async Task NormalNotification_WithChannelsOff_SendsNothing_ButRecordsInApp()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        var off = await _httpClient.PutAsJsonAsync("/api/communication-preferences/mine",
            new { smsEnabled = false, emailEnabled = false });
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);

        Sms.Sent.Clear();
        Email.Sent.Clear();

        using (var scope = _fixture.Services.CreateScope())
        {
            var notifier = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notifier.NotifyAsync(userId, NotificationType.General, "Booking update", "Your booking changed.");
        }

        // Opt-out honoured: no SMS/email dispatched.
        Assert.Empty(Sms.Sent);
        Assert.Empty(Email.Sent);

        // In-app record still created.
        using var verify = _fixture.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Notifications.AnyAsync(n => n.UserId == userId && !n.IsEmergencyOverride));
    }

    [Fact]
    public async Task EmergencyAlert_OverridesOptOut_AndFlagsNotification()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        await _httpClient.PutAsJsonAsync("/api/communication-preferences/mine",
            new { smsEnabled = false, emailEnabled = false });

        Sms.Sent.Clear();
        Email.Sent.Clear();

        var response = await _httpClient.PostAsJsonAsync("/api/safety/alert", new { bookingId = "x" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Emergency bypasses the opt-out on both channels (SMS, email).
        Assert.NotEmpty(Sms.Sent);
        Assert.NotEmpty(Email.Sent);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Notifications.AnyAsync(n =>
            n.UserId == userId && n.Type == NotificationType.SafetyAlert && n.IsEmergencyOverride));
    }
}
