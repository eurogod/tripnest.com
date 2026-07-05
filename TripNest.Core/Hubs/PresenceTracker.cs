using System.Collections.Concurrent;

namespace TripNest.Core.Hubs;

/// <summary>
/// Tracks which users currently have at least one live chat connection, so the API can report
/// online/offline. A user may have several connections (multiple tabs/devices); they count as online
/// until the last one drops.
/// <para>
/// This is in-memory and therefore per-instance. With multiple app instances behind the Redis
/// backplane, presence would need a shared store (e.g. Redis) to be globally accurate; today it
/// reflects connections on the local instance only.
/// </para>
/// </summary>
public interface IPresenceTracker
{
    /// <summary>Records a new connection. Returns true if the user just came online (first connection).</summary>
    bool Connect(string userId, string connectionId);

    /// <summary>Removes a connection. Returns true if the user just went offline (last connection dropped).</summary>
    bool Disconnect(string userId, string connectionId);

    bool IsOnline(string userId);
}

public sealed class PresenceTracker : IPresenceTracker
{
    // userId -> set of active connection ids.
    private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new();

    public bool Connect(string userId, string connectionId)
    {
        var wasOffline = false;
        _connections.AddOrUpdate(
            userId,
            _ => { wasOffline = true; return new HashSet<string> { connectionId }; },
            (_, existing) =>
            {
                lock (existing) { existing.Add(connectionId); }
                return existing;
            });
        return wasOffline;
    }

    public bool Disconnect(string userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var existing))
            return false;

        lock (existing)
        {
            existing.Remove(connectionId);
            if (existing.Count > 0)
                return false;

            // Last connection gone — remove the entry while still holding the set's lock. A
            // concurrent Connect for this user blocks on the same lock inside AddOrUpdate's update
            // path; once the entry is removed, its TryUpdate fails and AddOrUpdate retries as a
            // fresh add (reporting the user online again), so the reconnect is never lost.
            _connections.TryRemove(new KeyValuePair<string, HashSet<string>>(userId, existing));
            return true;
        }
    }

    public bool IsOnline(string userId) =>
        _connections.TryGetValue(userId, out var set) && set.Count > 0;
}
