using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Pipster.Infrastructure.Idempotency;

/// <summary>
/// Redis-backed idempotency store for distributed deduplication.
/// Uses SET NX (set if not exists) for atomic check-and-set operations.
/// </summary>
public class RedisIdempotencyStore : IIdempotencyStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisIdempotencyStore> _logger;
    private const string KeyPrefix = "pipster:idempotency:";

    public RedisIdempotencyStore(
        IConnectionMultiplexer redis,
        ILogger<RedisIdempotencyStore> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<bool> TryMarkAsProcessedAsync(string key, TimeSpan ttl, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var redisKey = GetRedisKey(key);

        try
        {
            // SET NX: Set key only if it doesn't exist
            // Returns true if key was set (first time), false if key already exists
            var wasSet = await db.StringSetAsync(
                redisKey,
                DateTimeOffset.UtcNow.ToString("O"), // Store timestamp for debugging
                ttl,
                When.NotExists);

            if (wasSet)
            {
                _logger.LogDebug("Marked message {Key} as processed (TTL: {TTL})", key, ttl);
            }
            else
            {
                _logger.LogDebug("Message {Key} already processed (duplicate)", key);
            }

            return wasSet;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking idempotency for key {Key}", key);
            // Fail open: allow processing on Redis errors to avoid blocking legitimate messages
            return true;
        }
    }

    public async Task<bool> WasProcessedAsync(string key, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var redisKey = GetRedisKey(key);

        try
        {
            return await db.KeyExistsAsync(redisKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if key {Key} was processed", key);
            // Fail open: assume not processed on errors
            return false;
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var redisKey = GetRedisKey(key);

        try
        {
            await db.KeyDeleteAsync(redisKey);
            _logger.LogDebug("Removed idempotency key {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing idempotency key {Key}", key);
        }
    }

    private static string GetRedisKey(string key) => $"{KeyPrefix}{key}";
}