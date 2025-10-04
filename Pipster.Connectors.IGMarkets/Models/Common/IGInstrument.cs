using System.Text.Json.Serialization;

namespace Pipster.Connectors.IGMarkets.Models.Common;
public record IGInstrument
{
    /// <summary>
    /// Market epic
    /// </summary>
    [JsonPropertyName("epic")]
    public string? Epic { get; init; }

    /// <summary>
    /// Instrument name
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Instrument type (e.g., "CURRENCIES", "COMMODITIES")
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// Market ID
    /// </summary>
    [JsonPropertyName("marketId")]
    public string? MarketId { get; init; }
}
