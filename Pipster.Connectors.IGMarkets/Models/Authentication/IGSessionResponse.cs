using System.Text.Json.Serialization;

namespace Pipster.Connectors.IGMarkets.Models.Authentication;

/// <summary>
/// Response from IG session creation
/// Note: Tokens are returned in response headers (CST, X-SECURITY-TOKEN)
/// </summary>
public record IGSessionResponse
{
    /// <summary>
    /// Client identifier
    /// </summary>
    [JsonPropertyName("clientId")]
    public string? ClientId { get; init; }

    /// <summary>
    /// Account ID
    /// </summary>
    [JsonPropertyName("accountId")]
    public string? AccountId { get; init; }

    /// <summary>
    /// Lightstreamer endpoint for streaming data
    /// </summary>
    [JsonPropertyName("lightstreamerEndpoint")]
    public string? LightstreamerEndpoint { get; init; }

    /// <summary>
    /// Server timezone offset
    /// </summary>
    [JsonPropertyName("timezoneOffset")]
    public int TimezoneOffset { get; init; }
}