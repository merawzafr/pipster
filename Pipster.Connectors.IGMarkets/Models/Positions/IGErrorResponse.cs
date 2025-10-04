using System.Text.Json.Serialization;

namespace Pipster.Connectors.IGMarkets.Models.Common;

/// <summary>
/// IG API error response
/// </summary>
public record IGErrorResponse
{
    /// <summary>
    /// Error code
    /// </summary>
    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Error message
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}