using System.Text.Json.Serialization;

namespace Pipster.Connectors.IGMarkets.Models.Positions;

/// <summary>
/// Response containing deal reference ID
/// </summary>
public record IGDealReference
{
    /// <summary>
    /// Unique deal reference to track the operation
    /// </summary>
    [JsonPropertyName("dealReference")]
    public required string DealReference { get; init; }
}