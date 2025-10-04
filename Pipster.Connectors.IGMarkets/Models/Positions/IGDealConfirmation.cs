using System.Text.Json.Serialization;

namespace Pipster.Connectors.IGMarkets.Models.Positions;

/// <summary>
/// Deal confirmation response (obtained by polling with deal reference)
/// </summary>
public record IGDealConfirmation
{
    /// <summary>
    /// Deal reference ID
    /// </summary>
    [JsonPropertyName("dealReference")]
    public string? DealReference { get; init; }

    /// <summary>
    /// Deal ID (unique identifier for the position)
    /// </summary>
    [JsonPropertyName("dealId")]
    public string? DealId { get; init; }

    /// <summary>
    /// Deal status: "ACCEPTED", "REJECTED", etc.
    /// </summary>
    [JsonPropertyName("dealStatus")]
    public string? DealStatus { get; init; }

    /// <summary>
    /// Epic the deal was for
    /// </summary>
    [JsonPropertyName("epic")]
    public string? Epic { get; init; }

    /// <summary>
    /// Direction: "BUY" or "SELL"
    /// </summary>
    [JsonPropertyName("direction")]
    public string? Direction { get; init; }

    /// <summary>
    /// Position size
    /// </summary>
    [JsonPropertyName("size")]
    public decimal? Size { get; init; }

    /// <summary>
    /// Execution price level
    /// </summary>
    [JsonPropertyName("level")]
    public decimal? Level { get; init; }

    /// <summary>
    /// Reason for rejection (if dealStatus is REJECTED)
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>
    /// Profit/loss amount
    /// </summary>
    [JsonPropertyName("profit")]
    public decimal? Profit { get; init; }

    /// <summary>
    /// Profit/loss currency
    /// </summary>
    [JsonPropertyName("profitCurrency")]
    public string? ProfitCurrency { get; init; }
}