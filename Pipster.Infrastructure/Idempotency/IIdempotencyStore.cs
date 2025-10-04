namespace Pipster.Infrastructure.Idempotency;

/// <summary>
/// Store for tracking processed messages to prevent duplicate processing.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Attempts to mark a message as processed.
    /// </summary>
    /// <param name="key">Unique identifier for the message (e.g., tenantId:channelId:messageId)</param>
    /// <param name="ttl">How long to remember this message was processed</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if this is the first time seeing this key, false if already processed</returns>
    Task<bool> TryMarkAsProcessedAsync(string key, TimeSpan ttl, CancellationToken ct);

    /// <summary>
    /// Checks if a message has already been processed.
    /// </summary>
    Task<bool> WasProcessedAsync(string key, CancellationToken ct);

    /// <summary>
    /// Removes a message from the processed set (for testing/cleanup).
    /// </summary>
    Task RemoveAsync(string key, CancellationToken ct);
}