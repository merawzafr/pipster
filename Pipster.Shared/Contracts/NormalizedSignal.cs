using Pipster.Shared.Enums;

namespace Pipster.Shared.Contracts
{
    public sealed record NormalizedSignal(
        string TenantId,
        string Source,              // telegram channel name/id
        string Symbol,              // e.g., XAUUSD
        OrderSide Side,
        decimal? Entry,             // null = market
        decimal? StopLoss,
        IReadOnlyList<decimal> TakeProfits,
        DateTimeOffset SeenAt,
        string RawText,
        string Hash                 // idempotency key
    );
}
