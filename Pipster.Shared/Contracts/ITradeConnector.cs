namespace Pipster.Shared.Contracts;

/// <summary>
/// Interface for broker connectors to place trades.
/// </summary>
public interface ITradeConnector
{
    string BrokerType { get; }
    bool IsInitialized { get; }
    Task InitializeAsync(string tenantId, IReadOnlyDictionary<string, string> credentials, CancellationToken ct = default);
    Task<string> PlaceOrderAsync(TradeCommand cmd, CancellationToken ct = default);
    Task<bool> ValidateConnectionAsync(CancellationToken ct = default);
}

/// <summary>
/// Factory interface for creating connector instances.
/// Each connector implementation provides its own factory.
/// </summary>
public interface ITradeConnectorProvider
{
    /// <summary>
    /// Broker type this provider creates (e.g., "IGMarkets", "OANDA")
    /// </summary>
    string BrokerType { get; }

    /// <summary>
    /// Creates a new connector instance
    /// </summary>
    ITradeConnector CreateConnector();
}

public sealed class DummyConnector : ITradeConnector
{
    public string BrokerType => throw new NotImplementedException();

    public bool IsInitialized => throw new NotImplementedException();

    public Task InitializeAsync(string tenantId, IReadOnlyDictionary<string, string> credentials, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<string> PlaceOrderAsync(TradeCommand cmd, CancellationToken ct)
        => Task.FromResult($"SIM-{cmd.Symbol}-{Guid.NewGuid():N}");
    public Task<bool> ValidateConnectionAsync(CancellationToken ct = default) => throw new NotImplementedException();
}
