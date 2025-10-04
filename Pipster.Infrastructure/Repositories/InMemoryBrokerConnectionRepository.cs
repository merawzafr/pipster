using System.Collections.Concurrent;
using Pipster.Domain.Entities;
using Pipster.Domain.Enums;
using Pipster.Domain.Repositories;

namespace Pipster.Infrastructure.Repositories;

/// <summary>
/// In-memory implementation of broker connection repository for MVP/testing.
/// Thread-safe using ConcurrentDictionary.
/// CAUTION: Data is lost on application restart - use only for development.
/// </summary>
public sealed class InMemoryBrokerConnectionRepository : IBrokerConnectionRepository
{
    private readonly ConcurrentDictionary<string, BrokerConnection> _connections = new();

    public Task<BrokerConnection?> GetByIdAsync(string connectionId, CancellationToken ct = default)
    {
        _connections.TryGetValue(connectionId, out var connection);
        return Task.FromResult(connection);
    }

    public Task<IReadOnlyList<BrokerConnection>> GetByTenantIdAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var connections = _connections.Values
            .Where(c => c.TenantId == tenantId)
            .ToList();
        return Task.FromResult<IReadOnlyList<BrokerConnection>>(connections);
    }

    public Task<IReadOnlyList<BrokerConnection>> GetActiveByTenantIdAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var connections = _connections.Values
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .ToList();
        return Task.FromResult<IReadOnlyList<BrokerConnection>>(connections);
    }

    public Task<IReadOnlyList<BrokerConnection>> GetByTenantAndTypeAsync(
        string tenantId,
        BrokerType brokerType,
        CancellationToken ct = default)
    {
        var connections = _connections.Values
            .Where(c => c.TenantId == tenantId && c.BrokerType == brokerType)
            .ToList();
        return Task.FromResult<IReadOnlyList<BrokerConnection>>(connections);
    }

    public Task AddAsync(BrokerConnection connection, CancellationToken ct = default)
    {
        if (!_connections.TryAdd(connection.Id, connection))
        {
            throw new InvalidOperationException(
                $"Broker connection with ID '{connection.Id}' already exists");
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(BrokerConnection connection, CancellationToken ct = default)
    {
        if (!_connections.ContainsKey(connection.Id))
        {
            throw new InvalidOperationException(
                $"Broker connection '{connection.Id}' not found");
        }

        _connections[connection.Id] = connection;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string connectionId, CancellationToken ct = default)
    {
        _connections.TryRemove(connectionId, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string connectionId, CancellationToken ct = default)
    {
        return Task.FromResult(_connections.ContainsKey(connectionId));
    }
}