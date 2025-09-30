using Pipster.Shared.Contracts;
using Pipster.Shared.Contracts.Telegram;

namespace Pipster.Infrastructure.Messaging
{
    public interface IMessageBus
    {
        // Existing methods
        Task PublishSignalAsync(NormalizedSignal signal, CancellationToken ct);
        IAsyncEnumerable<NormalizedSignal> ConsumeSignalsAsync(CancellationToken ct);

        Task PublishTradeAsync(TradeCommand cmd, CancellationToken ct);
        IAsyncEnumerable<TradeCommand> ConsumeTradesAsync(CancellationToken ct);

        // New Telegram methods
        Task PublishTelegramMessageAsync(TelegramMessageReceived message, CancellationToken ct);
        IAsyncEnumerable<TelegramMessageReceived> ConsumeTelegramMessagesAsync(CancellationToken ct);

        Task PublishChannelRequestAsync(AddChannelRequest request, CancellationToken ct);
        IAsyncEnumerable<AddChannelRequest> ConsumeChannelRequestsAsync(CancellationToken ct);

        Task PublishRemoveChannelRequestAsync(RemoveChannelRequest request, CancellationToken ct);
        IAsyncEnumerable<RemoveChannelRequest> ConsumeRemoveChannelRequestsAsync(CancellationToken ct);
    }
}