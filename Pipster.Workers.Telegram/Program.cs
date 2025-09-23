using Pipster.Infrastructure.Messaging;
using Pipster.Shared.Contracts;

// dotnet add package Telegram.Bot
// For MVP you can start with long polling.
await Host.CreateDefaultBuilder(args)
    .ConfigureServices(s =>
{
s.AddSingleton<IMessageBus, InMemoryBus>();
s.AddSingleton<ISignalParser, RegexSignalParser>();
s.AddHostedService<TelegramListener>();
s.AddHostedService<SignalToTradeProjector>(); // parses & emits TradeCommand
})
    .RunConsoleAsync();

public sealed class TelegramListener : BackgroundService
{
    private readonly ILogger<TelegramListener> _log;
    private readonly IMessageBus _bus;
    private readonly ISignalParser _parser;

    public TelegramListener(ILogger<TelegramListener> log, IMessageBus bus, ISignalParser parser)
    { _log = log; _bus = bus; _parser = parser; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // TODO: init Telegram.Bot with your token; subscribe to updates for chosen chats
        // For skeleton, feed a sample message:
        var sample = "Sell #XAUUSD 3689-3693 SL 3700 TP 3687 3685 3680";
        var parsed = _parser.TryParse("tenant-self", "my_channel", sample);
        if (parsed != null)
        {
            await _bus.PublishSignalAsync(parsed, ct);
            _log.LogInformation("Published signal {Hash}", parsed.Hash);
        }

        await Task.Delay(Timeout.Infinite, ct);
    }
}

public sealed class SignalToTradeProjector : BackgroundService
{
    private readonly IMessageBus _bus;
    private readonly ILogger<SignalToTradeProjector> _log;

    public SignalToTradeProjector(IMessageBus bus, ILogger<SignalToTradeProjector> log)
    { _bus = bus; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var sig in _bus.ConsumeSignalsAsync(ct))
        {
            // MVP sizing: fixed minimum units (e.g., 1 for CFDs, 100 for FX micro—adjust per broker)
            var units = 1;
            var cmd = new TradeCommand(
                sig.TenantId, sig.Symbol, sig.Side, units,
                sig.Entry, sig.StopLoss, sig.TakeProfits.FirstOrDefault(),
                sig.Hash, DateTimeOffset.UtcNow);

            await _bus.PublishTradeAsync(cmd, ct);
            _log.LogInformation("Projected TradeCommand for {Symbol} ({Side})", sig.Symbol, sig.Side);
        }
    }
}
