using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Pipster.Shared.Contracts;

namespace Pipster.Infrastructure.Messaging
{
    public sealed class InMemoryBus : IMessageBus
    {
        private readonly Channel<NormalizedSignal> _signals = Channel.CreateUnbounded<NormalizedSignal>();
        private readonly Channel<TradeCommand> _trades = Channel.CreateUnbounded<TradeCommand>();

        public Task PublishSignalAsync(NormalizedSignal s, CancellationToken ct) => _signals.Writer.WriteAsync(s, ct).AsTask();
        public async IAsyncEnumerable<NormalizedSignal> ConsumeSignalsAsync([EnumeratorCancellation] CancellationToken ct)
        {
            while (await _signals.Reader.WaitToReadAsync(ct))
                while (_signals.Reader.TryRead(out var s)) yield return s;
        }

        public Task PublishTradeAsync(TradeCommand t, CancellationToken ct) => _trades.Writer.WriteAsync(t, ct).AsTask();
        public async IAsyncEnumerable<TradeCommand> ConsumeTradesAsync([EnumeratorCancellation] CancellationToken ct)
        {
            while (await _trades.Reader.WaitToReadAsync(ct))
                while (_trades.Reader.TryRead(out var t)) yield return t;
        }
    }
}
