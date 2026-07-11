using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.Enums;
using TripNest.Core.Models;
using TripNest.Core.Services;

namespace TripNest.Core.Tests.Controllers;

/// <summary>
/// Coverage for iCal import (cross-platform calendar sync): the RFC 5545 parser, linking a feed,
/// imported ranges blocking real bookings, re-sync replacing stale ranges, and unlink cleanup.
/// </summary>
public class ExternalCalendarSyncTests : TestBase
{
    // ---------------------------------------------------------------- parser

    [Fact]
    public void Parser_ReadsDateAndDateTimeEvents_UnfoldsLines_AndDefaultsMissingDtend()
    {
        const string ics = "BEGIN:VCALENDAR\r\n" +
                           "BEGIN:VEVENT\r\n" +
                           "DTSTART;VALUE=DATE:20260801\r\n" +
                           "DTEND;VALUE=DATE:20260805\r\n" +
                           "SUMMARY:Reserved on\r\n" +
                           " Airbnb\r\n" + // folded continuation line
                           "END:VEVENT\r\n" +
                           "BEGIN:VEVENT\r\n" +
                           "DTSTART:20260910T140000Z\r\n" +
                           "END:VEVENT\r\n" +
                           "END:VCALENDAR\r\n";

        var ranges = IcalParser.ParseBusyRanges(ics);

        Assert.Equal(2, ranges.Count);
        Assert.Equal(new DateTime(2026, 8, 1), ranges[0].Start);
        Assert.Equal(new DateTime(2026, 8, 5), ranges[0].End);
        // Missing DTEND => one-day busy range.
        Assert.Equal(new DateTime(2026, 9, 10), ranges[1].Start);
        Assert.Equal(new DateTime(2026, 9, 11), ranges[1].End);
    }

    // ------------------------------------------------------------- lifecycle

    [Fact]
    public async Task ImportedFeed_BlocksBookings_ResyncReplaces_UnlinkCleansUp()
    {
        var propertyId = await CreateActivePropertyAsync();
        var stub = _fixture.Services.GetRequiredService<StubIcalFeedFetcher>();

        var busyStart = DateTime.UtcNow.Date.AddDays(40);
        const string feedUrl = "https://airbnb.example.com/calendar/abc123.ics";
        stub.Feeds[feedUrl] = Vcal((busyStart, busyStart.AddDays(3)));

        // Link the feed — the first import runs immediately.
        var added = await DataOf(await _httpClient.PostAsJsonAsync(
            $"/api/calendar/{propertyId}/external", new { name = "Airbnb", feedUrl }));
        var calendarId = added.GetProperty("id").GetString()!;
        Assert.Equal(1, added.GetProperty("importedRanges").GetInt32());
        Assert.Equal(JsonValueKind.Null, added.GetProperty("lastSyncError").ValueKind);

        // The imported range blocks a real booking attempt (409 from availability).
        await RegisterAndLoginAsync(UserRole.Tenant);
        var conflict = await _httpClient.PostAsJsonAsync("/api/bookings", new
        {
            propertyId,
            checkInDate = busyStart.ToString("yyyy-MM-dd"),
            checkOutDate = busyStart.AddDays(2).ToString("yyyy-MM-dd"),
            guests = 1
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        // The other platform's guest checked out — the feed now shows a different range.
        // Re-sync (as the owner) must replace, not accumulate.
        await LoginAsOwnerAsync();
        var moved = busyStart.AddDays(30);
        stub.Feeds[feedUrl] = Vcal((moved, moved.AddDays(2)));
        var synced = await DataOf(await _httpClient.PostAsync($"/api/calendar/external/{calendarId}/sync", null));
        Assert.Equal(1, synced.GetProperty("importedRanges").GetInt32());

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var imported = db.PropertyBlockedDates.Where(d => d.ExternalCalendarId == calendarId).ToList();
            Assert.Single(imported);
            Assert.Equal(moved, imported[0].StartDate.Date);
        }

        // Unlink removes the imported blocks but leaves manual ones alone.
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.PropertyBlockedDates.Add(new PropertyBlockedDate
            {
                PropertyId = propertyId,
                BlockedByUserId = _ownerId!,
                StartDate = DateTime.UtcNow.Date.AddDays(90),
                EndDate = DateTime.UtcNow.Date.AddDays(92),
                Reason = "Manual block"
            });
            await db.SaveChangesAsync();
        }

        var delete = await _httpClient.DeleteAsync($"/api/calendar/external/{calendarId}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Empty(db.PropertyBlockedDates.Where(d => d.ExternalCalendarId == calendarId));
            Assert.Single(db.PropertyBlockedDates.Where(d => d.PropertyId == propertyId && d.Reason == "Manual block"));
        }
    }

    [Fact]
    public async Task AddFeed_RejectsPrivateAndNonHttpUrls()
    {
        var propertyId = await CreateActivePropertyAsync();

        foreach (var bad in new[] { "ftp://calendar.example.com/a.ics", "http://localhost/a.ics", "http://10.0.0.5/a.ics", "not-a-url" })
        {
            var res = await _httpClient.PostAsJsonAsync(
                $"/api/calendar/{propertyId}/external", new { name = "Airbnb", feedUrl = bad });
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }
    }

    [Fact]
    public async Task DeadFeed_ReportsErrorOnTheRow_InsteadOfFailing()
    {
        var propertyId = await CreateActivePropertyAsync();

        // No stub entry for this URL => the fetch throws, simulating a dead host.
        var added = await DataOf(await _httpClient.PostAsJsonAsync(
            $"/api/calendar/{propertyId}/external",
            new { name = "VRBO", feedUrl = "https://vrbo.example.com/dead.ics" }));

        Assert.Equal(0, added.GetProperty("importedRanges").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, added.GetProperty("lastSyncError").ValueKind);
    }

    // ---------------------------------------------------------------- helpers

    private string? _ownerId;
    private string? _ownerToken;

    private async Task<string> CreateActivePropertyAsync()
    {
        var (landlordId, token) = await RegisterAndLoginAsync(UserRole.Landlord);
        (_ownerId, _ownerToken) = (landlordId, token);
        await MarkUserVerifiedAsync(landlordId);

        var res = await _httpClient.PostAsJsonAsync("/api/properties", new CreatePropertyRequest
        {
            Title = "Synced Listing",
            Description = "Listed on multiple platforms",
            Location = "Accra, Ghana",
            Latitude = 5.6,
            Longitude = -0.19,
            Bedrooms = 1,
            Bathrooms = 1,
            MonthlyRent = 2000m,
            DailyRate = 90m,
            PropertyType = "Apartment",
            StayType = StayType.ShortTerm,
            CancellationPolicy = CancellationPolicy.Flexible
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var data = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        var propertyId = data.GetProperty("propertyId").GetString()!;

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await db.Properties.FindAsync(propertyId);
        property!.Status = PropertyStatus.Active;
        await db.SaveChangesAsync();
        return propertyId;
    }

    private Task LoginAsOwnerAsync()
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _ownerToken);
        return Task.CompletedTask;
    }

    private static string Vcal(params (DateTime Start, DateTime End)[] ranges)
    {
        var sb = new System.Text.StringBuilder("BEGIN:VCALENDAR\r\nVERSION:2.0\r\n");
        foreach (var (start, end) in ranges)
        {
            sb.Append("BEGIN:VEVENT\r\n");
            sb.Append($"DTSTART;VALUE=DATE:{start:yyyyMMdd}\r\n");
            sb.Append($"DTEND;VALUE=DATE:{end:yyyyMMdd}\r\n");
            sb.Append("SUMMARY:Reserved\r\n");
            sb.Append("END:VEVENT\r\n");
        }
        return sb.Append("END:VCALENDAR\r\n").ToString();
    }

    private static async Task<JsonElement> DataOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data");
    }
}
