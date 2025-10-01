using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Pipster.Infrastructure.Idempotency;

/// <summary>
/// In-memory idempotency store for development/testing.
/// NOT suitable for production (no persistence, no distributed support).
/// </summary>
public class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _processedKeys = new();
    private readonly ILogger<InMemoryIdempotencyStore> _logger;
    private readonly PeriodicTimer _cleanupTimer;
    private readonly Task _cleanupTask;
    private readonly CancellationTokenSource _cleanupCts = new();

    public InMemoryIdempotencyStore(ILogger<InMemoryIdempotencyStore> logger)
    {
        _logger = logger;
        _cleanupTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        _cleanupTask = RunCleanupAsync(_cleanupCts.Token);
    }

    public Task<bool> TryMarkAsProcessedAsync(string key, TimeSpan ttl, CancellationToken ct)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);

        // TryAdd returns true if key was added (first time), false if key already exists
        var wasAdded = _processedKeys.TryAdd(key, expiresAt);

        if (wasAdded)
        {
            _logger.LogDebug("Marked message {Key} as processed (expires: {ExpiresAt})", key, expiresAt);
        }
        else
        {
            _logger.LogDebug("Message {Key} already processed (duplicate)", key);
        }

        return Task.FromResult(wasAdded);
    }

    public Task<bool> WasProcessedAsync(string key, CancellationToken ct)
    {
        if (_processedKeys.TryGetValue(key, out var expiresAt))
        {
            // Check if expired
            if (DateTimeOffset.UtcNow > expiresAt)
            {
                _processedKeys.TryRemove(key, out _);
                return Task.FromResult(false);
            }
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task RemoveAsync(string key, CancellationToken ct)
    {
        _processedKeys.TryRemove(key, out _);
        _logger.LogDebug("Removed idempotency key {Key}", key);
        return Task.CompletedTask;
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        while (await _cleanupTimer.WaitForNextTickAsync(ct))
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var expiredKeys = _processedKeys
                    .Where(kvp => now > kvp.Value)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _processedKeys.TryRemove(key, out _);
                }

                if (expiredKeys.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired idempotency keys", expiredKeys.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during idempotency cleanup");
            }
        }
    }

    public void Dispose()
    {
        _cleanupCts.Cancel();
        _cleanupTimer.Dispose();
        _cleanupCts.Dispose();
    }
}