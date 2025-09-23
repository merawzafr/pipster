using Pipster.Shared.Contracts;

namespace Pipster.Infrastructure.Messaging
{
    public interface IMessageBus
    {
        Task PublishSignalAsync(NormalizedSignal signal, CancellationToken ct);
        IAsyncEnumerable<NormalizedSignal> ConsumeSignalsAsync(CancellationToken ct);

        Task PublishTradeAsync(TradeCommand cmd, CancellationToken ct);
        IAsyncEnumerable<TradeCommand> ConsumeTradesAsync(CancellationToken ct);
    }
}
