using Microsoft.AspNetCore.OutputCaching;
using StackExchange.Redis;

namespace TripNest.Core.Caching;

/// <summary>
/// Distributed <see cref="IOutputCacheStore"/> backed by Redis, so cached responses are shared across
/// every API instance (the default store is per-process). Registered only when Redis is configured;
/// otherwise the in-memory default store is used. Tag membership is tracked in Redis sets so
/// <see cref="EvictByTagAsync"/> works if tag-based eviction is added later.
/// </summary>
public sealed class RedisOutputCacheStore : IOutputCacheStore
{
    private const string EntryPrefix = "tn:oc:";
    private const string TagPrefix = "tn:oct:";

    private readonly IConnectionMultiplexer _mux;

    public RedisOutputCacheStore(IConnectionMultiplexer mux) => _mux = mux;

    public async ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var value = await _mux.GetDatabase().StringGetAsync(EntryPrefix + key);
        return value.HasValue ? (byte[]?)value : null;
    }

    public async ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken cancellationToken)
    {
        var db = _mux.GetDatabase();
        await db.StringSetAsync(EntryPrefix + key, value, validFor);

        if (tags is { Length: > 0 })
        {
            foreach (var tag in tags)
            {
                await db.SetAddAsync(TagPrefix + tag, key);
                // Keep the tag index slightly longer than the entry so eviction can still find the key.
                await db.KeyExpireAsync(TagPrefix + tag, validFor + TimeSpan.FromMinutes(5));
            }
        }
    }

    public async ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken)
    {
        var db = _mux.GetDatabase();
        var keys = await db.SetMembersAsync(TagPrefix + tag);
        foreach (var k in keys)
            await db.KeyDeleteAsync(EntryPrefix + (string?)k);
        await db.KeyDeleteAsync(TagPrefix + tag);
    }
}
