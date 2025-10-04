using System.Text.Json.Serialization;

namespace Pipster.Connectors.IGMarkets.Models.Common;

/// <summary>
/// Market details for an instrument
/// </summary>
public record IGMarketDetails
{
    /// <summary>
    /// Instrument details
    /// </summary>
    [JsonPropertyName("instrument")]
    public IGInstrument? Instrument { get; init; }

    /// <summary>
    /// Dealing rules
    /// </summary>
    [JsonPropertyName("dealingRules")]
    public IGDealingRules? DealingRules { get; init; }

    /// <summary>
    /// Market snapshot (current prices)
    /// </summary>
    [JsonPropertyName("snapshot")]
    public IGSnapshot? Snapshot { get; init; }
}