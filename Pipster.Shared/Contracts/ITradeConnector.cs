using Pipster.Shared.Contracts;

/// <summary>
/// Interface for broker connectors to place trades.
/// Each connector instance is initialized with specific tenant credentials.
/// </summary>
public interface ITradeConnector
{
    /// <summary>
    /// Broker type identifier (e.g., "IGMarkets", "OANDA")
    /// </summary>
    string BrokerType { get; }

    /// <summary>
    /// Whether this connector is initialized and ready to trade
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Initializes connector with tenant-specific credentials
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="credentials">Decrypted credentials dictionary</param>
    /// <param name="ct">Cancellation token</param>
    Task InitializeAsync(
        string tenantId,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken ct = default);

    /// <summary>
    /// Places an order with the broker
    /// </summary>
    /// <param name="cmd">Trade command with order details</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Broker's order ID</returns>
    Task<string> PlaceOrderAsync(TradeCommand cmd, CancellationToken ct = default);

    /// <summary>
    /// Validates that credentials work and connection is healthy
    /// </summary>
    Task<bool> ValidateConnectionAsync(CancellationToken ct = default);
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
