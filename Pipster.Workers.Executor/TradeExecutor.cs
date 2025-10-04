using Pipster.Infrastructure.Connectors;
using Pipster.Infrastructure.Messaging;

namespace Pipster.Workers.Executor;

/// <summary>
/// Background service that executes trades via broker connectors.
/// Uses factory pattern to support multiple brokers without knowing about them.
/// </summary>
public sealed class TradeExecutor : BackgroundService
{
    private readonly IMessageBus _bus;
    private readonly ITradeConnectorFactory _connectorFactory;
    private readonly ILogger<TradeExecutor> _log;

    public TradeExecutor(
        IMessageBus bus,
        ITradeConnectorFactory connectorFactory,
        ILogger<TradeExecutor> log)
    {
        _bus = bus;
        _connectorFactory = connectorFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("Trade Executor starting");

        await foreach (var cmd in _bus.ConsumeTradesAsync(ct))
        {
            try
            {
                // Get connector for this specific broker connection
                var connector = await _connectorFactory.GetConnectorAsync(
                    cmd.BrokerConnectionId,
                    ct);

                var brokerId = await connector.PlaceOrderAsync(cmd, ct);

                _log.LogInformation(
                    "Placed order {OrderId} on broker {BrokerConnectionId} for {Symbol} {Side}",
                    brokerId,
                    cmd.BrokerConnectionId,
                    cmd.Symbol,
                    cmd.Side);
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Failed to execute trade on broker {BrokerConnectionId} for {Symbol}",
                    cmd.BrokerConnectionId,
                    cmd.Symbol);
            }
        }
    }
}