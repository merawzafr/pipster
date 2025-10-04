using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pipster.Domain.Enums;
using Pipster.Domain.Repositories;
using Pipster.Shared.Contracts;

namespace Pipster.Infrastructure.Connectors;

/// <summary>
/// Factory implementation for creating and caching trade connectors.
/// Uses registered connector providers (dependency injection) to create instances.
/// </summary>
public sealed class TradeConnectorFactory : ITradeConnectorFactory
{
    private readonly IBrokerConnectionRepository _brokerRepo;
    private readonly IEnumerable<ITradeConnectorProvider> _connectorProviders;
    private readonly ILogger<TradeConnectorFactory> _logger;

    // Cache connectors by broker connection ID
    private readonly ConcurrentDictionary<string, ITradeConnector> _connectorCache = new();

    // Map broker type names to providers
    private readonly Dictionary<string, ITradeConnectorProvider> _providersByType;

    public TradeConnectorFactory(
        IBrokerConnectionRepository brokerRepo,
        IEnumerable<ITradeConnectorProvider> connectorProviders,
        ILogger<TradeConnectorFactory> logger)
    {
        _brokerRepo = brokerRepo;
        _connectorProviders = connectorProviders;
        _logger = logger;

        // Build provider lookup map
        _providersByType = _connectorProviders.ToDictionary(
            p => p.BrokerType,
            p => p,
            StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation(
            "Initialized connector factory with {Count} registered providers: {Providers}",
            _providersByType.Count,
            string.Join(", ", _providersByType.Keys));
    }

    public async Task<ITradeConnector> GetConnectorAsync(
        string brokerConnectionId,
        CancellationToken ct = default)
    {
        // Check cache first
        if (_connectorCache.TryGetValue(brokerConnectionId, out var cached))
        {
            _logger.LogDebug(
                "Returning cached connector for broker connection {BrokerConnectionId}",
                brokerConnectionId);
            return cached;
        }

        // Load broker connection from database
        var connection = await _brokerRepo.GetByIdAsync(brokerConnectionId, ct);
        if (connection == null)
        {
            throw new InvalidOperationException(
                $"Broker connection '{brokerConnectionId}' not found");
        }

        if (!connection.IsActive)
        {
            throw new InvalidOperationException(
                $"Broker connection '{brokerConnectionId}' is not active");
        }

        var brokerTypeName = connection.BrokerType.ToString();

        _logger.LogInformation(
            "Creating new connector for broker connection {BrokerConnectionId}, type: {BrokerType}",
            brokerConnectionId,
            brokerTypeName);

        // Get provider for this broker type
        if (!_providersByType.TryGetValue(brokerTypeName, out var provider))
        {
            throw new NotSupportedException(
                $"No connector provider registered for broker type '{brokerTypeName}'. " +
                $"Available providers: {string.Join(", ", _providersByType.Keys)}");
        }

        // Create connector instance
        var connector = provider.CreateConnector();

        // Decrypt credentials
        var credentials = DecryptCredentials(connection.EncryptedCredentials);

        // Initialize connector
        await connector.InitializeAsync(connection.TenantId, credentials, ct);

        // Validate connection
        var isValid = await connector.ValidateConnectionAsync(ct);
        if (!isValid)
        {
            _logger.LogWarning(
                "Connector validation failed for broker connection {BrokerConnectionId}",
                brokerConnectionId);
            throw new InvalidOperationException(
                $"Failed to validate connection to {brokerTypeName} for broker connection '{brokerConnectionId}'");
        }

        // Update last used timestamp
        connection.MarkAsUsed();
        await _brokerRepo.UpdateAsync(connection, ct);

        // Cache and return
        _connectorCache.TryAdd(brokerConnectionId, connector);

        _logger.LogInformation(
            "Successfully created and cached connector for broker connection {BrokerConnectionId}",
            brokerConnectionId);

        return connector;
    }

    public async Task<IReadOnlyList<ITradeConnector>> GetConnectorsForTenantAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var connections = await _brokerRepo.GetActiveByTenantIdAsync(tenantId, ct);
        var connectors = new List<ITradeConnector>();

        foreach (var connection in connections)
        {
            try
            {
                var connector = await GetConnectorAsync(connection.Id, ct);
                connectors.Add(connector);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to get connector for broker connection {BrokerConnectionId}",
                    connection.Id);
            }
        }

        return connectors;
    }

    public void InvalidateConnector(string brokerConnectionId)
    {
        if (_connectorCache.TryRemove(brokerConnectionId, out _))
        {
            _logger.LogInformation(
                "Invalidated connector cache for broker connection {BrokerConnectionId}",
                brokerConnectionId);
        }
    }

    public void ClearAll()
    {
        var count = _connectorCache.Count;
        _connectorCache.Clear();
        _logger.LogInformation("Cleared all cached connectors ({Count})", count);
    }

    private IReadOnlyDictionary<string, string> DecryptCredentials(string encryptedCredentials)
    {
        // TODO: Implement proper decryption using Azure Key Vault or similar
        // For now, assume credentials are stored as JSON (NOT SECURE - FIX IN PRODUCTION)

        try
        {
            var credentials = JsonSerializer.Deserialize<Dictionary<string, string>>(encryptedCredentials);
            return credentials ?? new Dictionary<string, string>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize credentials");
            throw new InvalidOperationException("Invalid credential format", ex);
        }
    }
}