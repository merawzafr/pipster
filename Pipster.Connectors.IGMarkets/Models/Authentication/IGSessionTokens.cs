namespace Pipster.Connectors.IGMarkets.Models.Authentication;

/// <summary>
/// IG session tokens (extracted from response headers)
/// </summary>
public record IGSessionTokens
{
    /// <summary>
    /// Client Session Token (CST header)
    /// </summary>
    public required string ClientSessionToken { get; init; }

    /// <summary>
    /// Security token (X-SECURITY-TOKEN header)
    /// </summary>
    public required string SecurityToken { get; init; }

    /// <summary>
    /// When this session was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Estimated expiry time (IG sessions last ~6 hours)
    /// </summary>
    public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.AddHours(6);

    /// <summary>
    /// Check if tokens are still valid
    /// </summary>
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt.AddMinutes(-5); // 5 min buffer
}