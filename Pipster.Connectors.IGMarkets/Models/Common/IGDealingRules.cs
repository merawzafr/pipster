using System.Text.Json.Serialization;

namespace Pipster.Connectors.IGMarkets.Models.Common;

public record IGDealingRules
{
    /// <summary>
    /// Minimum deal size
    /// </summary>
    [JsonPropertyName("minDealSize")]
    public IGValue? MinDealSize { get; init; }

    /// <summary>
    /// Maximum deal size
    /// </summary>
    [JsonPropertyName("maxDealSize")]
    public IGValue? MaxDealSize { get; init; }
}
