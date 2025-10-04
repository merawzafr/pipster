using System.Collections.Concurrent;
using Pipster.Domain.Entities;
using Pipster.Domain.Repositories;

namespace Pipster.Infrastructure.Repositories;

/// <summary>
/// In-memory implementation of trading configuration repository for MVP/testing.
/// Thread-safe using ConcurrentDictionary.
/// CAUTION: Data is lost on application restart - use only for development.
/// </summary>
public sealed class InMemoryTradingConfigurationRepository : ITradingConfigurationRepository
{
    private readonly ConcurrentDictionary<string, TradingConfiguration> _configs = new();
    private readonly ConcurrentDictionary<string, string> _tenantIndex = new();

    public Task<TradingConfiguration?> GetByIdAsync(string configId, CancellationToken ct = default)
    {
        _configs.TryGetValue(configId, out var config);
        return Task.FromResult(config);
    }

    public Task<TradingConfiguration?> GetByTenantIdAsync(string tenantId, CancellationToken ct = default)
    {
        if (_tenantIndex.TryGetValue(tenantId, out var configId))
        {
            return GetByIdAsync(configId, ct);
        }
        return Task.FromResult<TradingConfiguration?>(null);
    }

    public Task<IReadOnlyList<TradingConfiguration>> GetAllAsync(CancellationToken ct = default)
    {
        var configs = _configs.Values.ToList();
        return Task.FromResult<IReadOnlyList<TradingConfiguration>>(configs);
    }

    public Task AddAsync(TradingConfiguration config, CancellationToken ct = default)
    {
        if (!_configs.TryAdd(config.Id, config))
        {
            throw new InvalidOperationException($"Trading configuration with ID '{config.Id}' already exists");
        }

        _tenantIndex.TryAdd(config.TenantId, config.Id);

        return Task.CompletedTask;
    }

    public Task UpdateAsync(TradingConfiguration config, CancellationToken ct = default)
    {
        if (!_configs.ContainsKey(config.Id))
        {
            throw new InvalidOperationException($"Trading configuration '{config.Id}' not found");
        }

        _configs[config.Id] = config;

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string configId, CancellationToken ct = default)
    {
        if (_configs.TryRemove(configId, out var config))
        {
            _tenantIndex.TryRemove(config.TenantId, out _);
        }

        return Task.CompletedTask;
    }

    public Task DeleteByTenantIdAsync(string tenantId, CancellationToken ct = default)
    {
        if (_tenantIndex.TryGetValue(tenantId, out var configId))
        {
            _configs.TryRemove(configId, out _);
            _tenantIndex.TryRemove(tenantId, out _);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsForTenantAsync(string tenantId, CancellationToken ct = default)
    {
        return Task.FromResult(_tenantIndex.ContainsKey(tenantId));
    }
}