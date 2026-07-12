using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.Enums;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Regression coverage for the production 500 on verification retry: a REJECTED attempt holds
/// the card's unique row, so restarting must reuse that row (never insert a duplicate), a card
/// on another account must be a clean 400, and same-row retries are cooldown-throttled because
/// the row-count rate limit can't see them.
/// </summary>
public class VerificationRetryTests : TestBase
{
    private const string Card = "GHA-111222333-4";

    private static object StartBody(string card = Card) => new
    {
        ghanaCardNumber = card,
        selfiePhotoPath = "/tmp/selfie.jpg",
        firstName = "Ama",
        lastName = "Mensah",
        dateOfBirth = "1995-05-05"
    };

    [Fact]
    public async Task RejectedVerification_CanBeRetried_ReusingTheSameRow()
    {
        var (userId, _) = await RegisterAndLoginAsync(UserRole.Tenant);

        // First attempt — then the background processor rejects it (e.g. NIA ServiceError).
        var first = await DataOf(await _httpClient.PostAsJsonAsync("/api/Verification/start", StartBody()));
        var verificationId = first.GetProperty("verificationId").GetString()!;

        // Let the background processor finish before overriding the row, or its save races ours.
        await WaitUntilProcessedAsync(verificationId);
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.VerificationRequests.FirstAsync(v => v.Id == verificationId);
            row.Status = VerificationStatus.Rejected;
            row.FailureReason = "Ghana card could not be verified (status: ServiceError)";
            row.SubmittedAt = DateTime.UtcNow.AddMinutes(-10); // past the retry cooldown
            await db.SaveChangesAsync();
        }

        // The retry that used to 500 on IX_VerificationRequests_GhanaCardNumber: it must
        // succeed by resetting the SAME row back to Pending.
        var retry = await DataOf(await _httpClient.PostAsJsonAsync("/api/Verification/start", StartBody()));
        Assert.Equal(verificationId, retry.GetProperty("verificationId").GetString());
        Assert.Equal((int)VerificationStatus.Pending, retry.GetProperty("status").GetInt32());

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.VerificationRequests.CountAsync(v => v.UserId == userId));
        }
    }

    [Fact]
    public async Task ImmediateRetry_IsCooldownThrottled()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        var first = await DataOf(await _httpClient.PostAsJsonAsync("/api/Verification/start", StartBody()));
        var verificationId = first.GetProperty("verificationId").GetString()!;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.VerificationRequests.FirstAsync(v => v.Id == verificationId);
            row.Status = VerificationStatus.Rejected; // rejected seconds ago
            await db.SaveChangesAsync();
        }

        // Same-row retries bypass the row-count rate limit, so the cooldown must catch them.
        var tooSoon = await _httpClient.PostAsJsonAsync("/api/Verification/start", StartBody());
        Assert.Equal(HttpStatusCode.TooManyRequests, tooSoon.StatusCode);
    }

    [Fact]
    public async Task CardAlreadyOnAnotherAccount_IsAFriendly400_Not500()
    {
        await RegisterAndLoginAsync(UserRole.Tenant);
        await DataOf(await _httpClient.PostAsJsonAsync("/api/Verification/start", StartBody()));

        // A different user claims the same card number.
        await RegisterAndLoginAsync(UserRole.Tenant);
        var stolen = await _httpClient.PostAsJsonAsync("/api/Verification/start", StartBody());
        Assert.Equal(HttpStatusCode.BadRequest, stolen.StatusCode);
        var body = await stolen.Content.ReadAsStringAsync();
        Assert.Contains("another account", body);
    }

    private async Task WaitUntilProcessedAsync(string verificationId)
    {
        for (var i = 0; i < 50; i++)
        {
            using var scope = _fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.VerificationRequests.AsNoTracking().FirstAsync(v => v.Id == verificationId);
            if (row.Status != VerificationStatus.Pending)
                return;
            await Task.Delay(100);
        }
    }

    private static async Task<JsonElement> DataOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data");
    }
}
