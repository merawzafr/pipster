using Pipster.Shared.Contracts;

namespace Pipster.Infrastructure.Connectors;

/// <summary>
/// Factory for creating and managing trade connector instances.
/// Handles credential decryption and connector lifecycle per broker connection.
/// </summary>
public interface ITradeConnectorFactory
{
    /// <summary>
    /// Gets or creates a connector for a specific broker connection
    /// </summary>
    /// <param name="brokerConnectionId">Broker connection ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Initialized trade connector</returns>
    Task<ITradeConnector> GetConnectorAsync(
        string brokerConnectionId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all active connectors for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of initialized connectors</returns>
    Task<IReadOnlyList<ITradeConnector>> GetConnectorsForTenantAsync(
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a connector from cache (forces recreation on next use)
    /// </summary>
    /// <param name="brokerConnectionId">Broker connection ID</param>
    void InvalidateConnector(string brokerConnectionId);

    /// <summary>
    /// Clears all cached connectors
    /// </summary>
    void ClearAll();
}