using TripNest.Core.Hubs;

namespace TripNest.Core.Tests;

/// <summary>
/// Unit coverage for the in-memory chat presence tracker: the online/offline transition is
/// reported exactly once per edge (first connection up, last connection down), regardless of
/// how many tabs/devices the user has open.
/// </summary>
public class PresenceTrackerTests
{
    private readonly PresenceTracker _tracker = new();

    [Fact]
    public void Connect_FirstConnection_ReportsCameOnline()
    {
        Assert.True(_tracker.Connect("user-1", "conn-a"));
        Assert.True(_tracker.IsOnline("user-1"));
    }

    [Fact]
    public void Connect_SecondConnection_DoesNotReportAgain()
    {
        _tracker.Connect("user-1", "conn-a");

        Assert.False(_tracker.Connect("user-1", "conn-b"));
        Assert.True(_tracker.IsOnline("user-1"));
    }

    [Fact]
    public void Disconnect_WithAnotherConnectionStillOpen_DoesNotReportOffline()
    {
        _tracker.Connect("user-1", "conn-a");
        _tracker.Connect("user-1", "conn-b");

        Assert.False(_tracker.Disconnect("user-1", "conn-a"));
        Assert.True(_tracker.IsOnline("user-1"));
    }

    [Fact]
    public void Disconnect_LastConnection_ReportsWentOffline()
    {
        _tracker.Connect("user-1", "conn-a");
        _tracker.Connect("user-1", "conn-b");
        _tracker.Disconnect("user-1", "conn-a");

        Assert.True(_tracker.Disconnect("user-1", "conn-b"));
        Assert.False(_tracker.IsOnline("user-1"));
    }

    [Fact]
    public void Disconnect_UnknownUser_ReturnsFalse()
    {
        Assert.False(_tracker.Disconnect("nobody", "conn-a"));
    }

    [Fact]
    public void Reconnect_AfterGoingOffline_ReportsCameOnlineAgain()
    {
        _tracker.Connect("user-1", "conn-a");
        _tracker.Disconnect("user-1", "conn-a");

        Assert.True(_tracker.Connect("user-1", "conn-b"));
        Assert.True(_tracker.IsOnline("user-1"));
    }

    [Fact]
    public void IsOnline_UnknownUser_IsFalse()
    {
        Assert.False(_tracker.IsOnline("nobody"));
    }
}
