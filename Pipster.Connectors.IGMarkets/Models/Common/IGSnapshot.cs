using System.Text.Json.Serialization;

namespace Pipster.Connectors.IGMarkets.Models.Common;

public record IGSnapshot
{
    /// <summary>
    /// Current bid price
    /// </summary>
    [JsonPropertyName("bid")]
    public decimal? Bid { get; init; }

    /// <summary>
    /// Current offer/ask price
    /// </summary>
    [JsonPropertyName("offer")]
    public decimal? Offer { get; init; }

    /// <summary>
    /// Market status (e.g., "TRADEABLE", "CLOSED")
    /// </summary>
    [JsonPropertyName("marketStatus")]
    public string? MarketStatus { get; init; }
}