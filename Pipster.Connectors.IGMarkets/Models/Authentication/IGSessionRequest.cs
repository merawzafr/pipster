using System.Text.Json.Serialization;

namespace Pipster.Connectors.IGMarkets.Models.Authentication;

/// <summary>
/// Request to create a new IG session
/// </summary>
public record IGSessionRequest
{
    /// <summary>
    /// IG account username
    /// </summary>
    [JsonPropertyName("identifier")]
    public required string Identifier { get; init; }

    /// <summary>
    /// IG account password
    /// </summary>
    [JsonPropertyName("password")]
    public required string Password { get; init; }
}