namespace Pipster.Connectors.IGMarkets.Configuration;

/// <summary>
/// Configuration options for IG Markets API connection
/// </summary>
public record IGMarketsOptions
{
    /// <summary>
    /// API key from IG Labs (https://labs.ig.com/)
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// IG demo/live account username
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// IG demo/live account password
    /// </summary>
    public required string Password { get; init; }

    /// <summary>
    /// Base URL for IG API
    /// Demo: https://demo-api.ig.com/gateway/deal
    /// Live: https://api.ig.com/gateway/deal
    /// </summary>
    public string BaseUrl { get; init; } = "https://demo-api.ig.com/gateway/deal";

    /// <summary>
    /// HTTP request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Enable automatic retry on transient failures
    /// </summary>
    public bool EnableRetries { get; init; } = true;

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Session token refresh interval in minutes (IG sessions last ~6 hours)
    /// </summary>
    public int SessionRefreshMinutes { get; init; } = 10;
}