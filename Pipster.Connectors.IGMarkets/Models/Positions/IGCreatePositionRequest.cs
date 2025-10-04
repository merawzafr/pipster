using System.Text.Json.Serialization;

namespace Pipster.Connectors.IGMarkets.Models.Positions;

/// <summary>
/// Request to create a new position (place a trade) on IG
/// </summary>
public record IGCreatePositionRequest
{
    /// <summary>
    /// IG market epic (e.g., CS.D.CFDGOLD.CFD.IP)
    /// </summary>
    [JsonPropertyName("epic")]
    public required string Epic { get; init; }

    /// <summary>
    /// Trade direction: "BUY" or "SELL"
    /// </summary>
    [JsonPropertyName("direction")]
    public required string Direction { get; init; }

    /// <summary>
    /// Position size (number of contracts/lots)
    /// </summary>
    [JsonPropertyName("size")]
    public required decimal Size { get; init; }

    /// <summary>
    /// Order type: "MARKET" or "LIMIT"
    /// </summary>
    [JsonPropertyName("orderType")]
    public required string OrderType { get; init; }

    /// <summary>
    /// Entry price level (required for LIMIT orders, ignored for MARKET)
    /// </summary>
    [JsonPropertyName("level")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Level { get; init; }

    /// <summary>
    /// Stop loss configuration
    /// </summary>
    [JsonPropertyName("stopLevel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? StopLevel { get; init; }

    /// <summary>
    /// Take profit configuration
    /// </summary>
    [JsonPropertyName("limitLevel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? LimitLevel { get; init; }

    /// <summary>
    /// Whether to use guaranteed stop loss (costs extra)
    /// </summary>
    [JsonPropertyName("guaranteedStop")]
    public bool GuaranteedStop { get; init; } = false;

    /// <summary>
    /// Force open new position even if existing position exists
    /// </summary>
    [JsonPropertyName("forceOpen")]
    public bool ForceOpen { get; init; } = true;

    /// <summary>
    /// Currency code for the position (e.g., "USD")
    /// </summary>
    [JsonPropertyName("currencyCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CurrencyCode { get; init; }

    /// <summary>
    /// Time in force: "EXECUTE_AND_ELIMINATE" (immediate), "FILL_OR_KILL", etc.
    /// </summary>
    [JsonPropertyName("timeInForce")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TimeInForce { get; init; }
}