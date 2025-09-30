using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Pipster.Shared.Contracts;
using Pipster.Shared.Contracts.Telegram;

namespace Pipster.Infrastructure.Messaging
{
    public sealed class InMemoryBus : IMessageBus
    {
        private readonly Channel<NormalizedSignal> _signals = Channel.CreateUnbounded<NormalizedSignal>();
        private readonly Channel<TradeCommand> _trades = Channel.CreateUnbounded<TradeCommand>();
        private readonly Channel<TelegramMessageReceived> _telegramMessages = Channel.CreateUnbounded<TelegramMessageReceived>();
        private readonly Channel<AddChannelRequest> _addChannelRequests = Channel.CreateUnbounded<AddChannelRequest>();
        private readonly Channel<RemoveChannelRequest> _removeChannelRequests = Channel.CreateUnbounded<RemoveChannelRequest>();

        // Existing signal methods
        public Task PublishSignalAsync(NormalizedSignal s, CancellationToken ct)
            => _signals.Writer.WriteAsync(s, ct).AsTask();

        public async IAsyncEnumerable<NormalizedSignal> ConsumeSignalsAsync([EnumeratorCancellation] CancellationToken ct)
        {
            while (await _signals.Reader.WaitToReadAsync(ct))
                while (_signals.Reader.TryRead(out var s)) yield return s;
        }

        // Existing trade methods
        public Task PublishTradeAsync(TradeCommand t, CancellationToken ct)
            => _trades.Writer.WriteAsync(t, ct).AsTask();

        public async IAsyncEnumerable<TradeCommand> ConsumeTradesAsync([EnumeratorCancellation] CancellationToken ct)
        {
            while (await _trades.Reader.WaitToReadAsync(ct))
                while (_trades.Reader.TryRead(out var t)) yield return t;
        }

        // New Telegram message methods
        public Task PublishTelegramMessageAsync(TelegramMessageReceived message, CancellationToken ct)
            => _telegramMessages.Writer.WriteAsync(message, ct).AsTask();

        public async IAsyncEnumerable<TelegramMessageReceived> ConsumeTelegramMessagesAsync([EnumeratorCancellation] CancellationToken ct)
        {
            while (await _telegramMessages.Reader.WaitToReadAsync(ct))
                while (_telegramMessages.Reader.TryRead(out var msg)) yield return msg;
        }

        // New channel request methods
        public Task PublishChannelRequestAsync(AddChannelRequest request, CancellationToken ct)
            => _addChannelRequests.Writer.WriteAsync(request, ct).AsTask();

        public async IAsyncEnumerable<AddChannelRequest> ConsumeChannelRequestsAsync([EnumeratorCancellation] CancellationToken ct)
        {
            while (await _addChannelRequests.Reader.WaitToReadAsync(ct))
                while (_addChannelRequests.Reader.TryRead(out var req)) yield return req;
        }

        // New remove channel request methods
        public Task PublishRemoveChannelRequestAsync(RemoveChannelRequest request, CancellationToken ct)
            => _removeChannelRequests.Writer.WriteAsync(request, ct).AsTask();

        public async IAsyncEnumerable<RemoveChannelRequest> ConsumeRemoveChannelRequestsAsync([EnumeratorCancellation] CancellationToken ct)
        {
            while (await _removeChannelRequests.Reader.WaitToReadAsync(ct))
                while (_removeChannelRequests.Reader.TryRead(out var req)) yield return req;
        }
    }
}