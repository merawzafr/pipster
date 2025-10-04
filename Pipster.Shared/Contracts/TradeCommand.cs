using Pipster.Shared.Enums;

namespace Pipster.Shared.Contracts
{
    public sealed record TradeCommand(
        string TenantId,
        string Symbol,
        OrderSide Side,
        int Units,                  // broker-minimum or sized
        decimal? Price,             // null = market
        decimal? StopLoss,
        decimal? TakeProfit,        // choose TP1 for MVP
        string CorrelationId,       // from NormalizedSignal.Hash
        DateTimeOffset CreatedAt,
        string BrokerConnectionId,  // Which broker connection to use
        string? SourceChannelId     // Which channel generated this signal (optional, for tracking)
    );
}