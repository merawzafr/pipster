using System.Text.Json.Serialization;

namespace Pipster.Connectors.IGMarkets.Models.Common;

public record IGValue
{
    /// <summary>
    /// Numeric value
    /// </summary>
    [JsonPropertyName("value")]
    public decimal Value { get; init; }

    /// <summary>
    /// Unit (e.g., "AMOUNT", "PERCENTAGE")
    /// </summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; init; }
}
