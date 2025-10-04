using System.Collections.Concurrent;
using Pipster.Domain.Entities;
using Pipster.Domain.Repositories;

namespace Pipster.Infrastructure.Repositories;

/// <summary>
/// In-memory implementation of channel configuration repository for MVP/testing.
/// Thread-safe using ConcurrentDictionary.
/// CAUTION: Data is lost on application restart - use only for development.
/// </summary>
public sealed class InMemoryChannelConfigurationRepository : IChannelConfigurationRepository
{
    private readonly ConcurrentDictionary<string, ChannelConfiguration> _configs = new();
    private readonly ConcurrentDictionary<string, string> _tenantChannelIndex = new();

    public Task<ChannelConfiguration?> GetByIdAsync(string configId, CancellationToken ct = default)
    {
        _configs.TryGetValue(configId, out var config);
        return Task.FromResult(config);
    }

    public Task<ChannelConfiguration?> GetByTenantAndChannelAsync(
        string tenantId,
        long channelId,
        CancellationToken ct = default)
    {
        var key = GetTenantChannelKey(tenantId, channelId);
        if (_tenantChannelIndex.TryGetValue(key, out var configId))
        {
            return GetByIdAsync(configId, ct);
        }
        return Task.FromResult<ChannelConfiguration?>(null);
    }

    public Task<IReadOnlyList<ChannelConfiguration>> GetByTenantIdAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var configs = _configs.Values
            .Where(c => c.TenantId == tenantId)
            .ToList();
        return Task.FromResult<IReadOnlyList<ChannelConfiguration>>(configs);
    }

    public Task<IReadOnlyList<ChannelConfiguration>> GetEnabledByTenantIdAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var configs = _configs.Values
            .Where(c => c.TenantId == tenantId && c.IsEnabled)
            .ToList();
        return Task.FromResult<IReadOnlyList<ChannelConfiguration>>(configs);
    }

    public Task<IReadOnlyList<ChannelConfiguration>> GetByChannelIdAsync(
        long channelId,
        CancellationToken ct = default)
    {
        var configs = _configs.Values
            .Where(c => c.ChannelId == channelId)
            .ToList();
        return Task.FromResult<IReadOnlyList<ChannelConfiguration>>(configs);
    }

    public Task AddAsync(ChannelConfiguration config, CancellationToken ct = default)
    {
        if (!_configs.TryAdd(config.Id, config))
        {
            throw new InvalidOperationException($"Channel configuration with ID '{config.Id}' already exists");
        }

        var key = GetTenantChannelKey(config.TenantId, config.ChannelId);
        _tenantChannelIndex.TryAdd(key, config.Id);

        return Task.CompletedTask;
    }

    public Task UpdateAsync(ChannelConfiguration config, CancellationToken ct = default)
    {
        if (!_configs.ContainsKey(config.Id))
        {
            throw new InvalidOperationException($"Channel configuration '{config.Id}' not found");
        }

        _configs[config.Id] = config;

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string configId, CancellationToken ct = default)
    {
        if (_configs.TryRemove(configId, out var config))
        {
            var key = GetTenantChannelKey(config.TenantId, config.ChannelId);
            _tenantChannelIndex.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task DeleteByTenantIdAsync(string tenantId, CancellationToken ct = default)
    {
        var configsToDelete = _configs.Values
            .Where(c => c.TenantId == tenantId)
            .ToList();

        foreach (var config in configsToDelete)
        {
            _configs.TryRemove(config.Id, out _);
            var key = GetTenantChannelKey(config.TenantId, config.ChannelId);
            _tenantChannelIndex.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string tenantId, long channelId, CancellationToken ct = default)
    {
        var key = GetTenantChannelKey(tenantId, channelId);
        return Task.FromResult(_tenantChannelIndex.ContainsKey(key));
    }

    private static string GetTenantChannelKey(string tenantId, long channelId)
        => $"{tenantId}:{channelId}";
}