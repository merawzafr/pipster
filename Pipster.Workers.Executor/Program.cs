using Pipster.Infrastructure.Messaging;
using Pipster.Shared.Contracts;

internal class Program
{
    private static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
    .ConfigureServices(s =>
{
    s.AddSingleton<IMessageBus, InMemoryBus>(); // swap to ServiceBus later
    s.AddSingleton<ITradeConnector, DummyConnector>(); // swap to OandaConnector
    s.AddHostedService<TradeExecutor>();
})
    .RunConsoleAsync();
    }
}

public interface ITradeConnector
{
    Task<string> PlaceOrderAsync(TradeCommand cmd, CancellationToken ct);
}

public sealed class DummyConnector : ITradeConnector
{
    public Task<string> PlaceOrderAsync(TradeCommand cmd, CancellationToken ct)
        => Task.FromResult($"SIM-{cmd.Symbol}-{Guid.NewGuid():N}");
}

public sealed class TradeExecutor(IMessageBus bus, ITradeConnector connector, ILogger<TradeExecutor> log) : BackgroundService
{
    private readonly IMessageBus _bus = bus;
    private readonly ITradeConnector _connector = connector;
    private readonly ILogger<TradeExecutor> _log = log;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var cmd in _bus.ConsumeTradesAsync(ct))
        {
            var brokerId = await _connector.PlaceOrderAsync(cmd, ct);
            _log.LogInformation("Placed order {Id} for {Symbol}", brokerId, cmd.Symbol);
        }
    }
}
