using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Pipster.Domain.Entities;
using Pipster.Domain.Enums;
using Pipster.Domain.Repositories;

namespace Pipster.Application.Services;

/// <summary>
/// Cached tenant configuration provider for high-performance message processing.
/// Uses in-memory cache with 5-minute TTL to reduce database load.
/// </summary>
public sealed class CachedTenantConfigProvider : ICachedTenantConfigProvider
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IChannelConfigurationRepository _channelRepository;
    private readonly ITradingConfigurationRepository _tradingRepository;
    private readonly ILogger<CachedTenantConfigProvider> _logger;

    private readonly ConcurrentDictionary<string, CachedTenant> _tenantCache = new();
    private readonly ConcurrentDictionary<string, CachedChannelConfig> _channelCache = new();
    private readonly ConcurrentDictionary<string, CachedTradingConfig> _tradingCache = new();

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public CachedTenantConfigProvider(
        ITenantRepository tenantRepository,
        IChannelConfigurationRepository channelRepository,
        ITradingConfigurationRepository tradingRepository,
        ILogger<CachedTenantConfigProvider> logger)
    {
        _tenantRepository = tenantRepository;
        _channelRepository = channelRepository;
        _tradingRepository = tradingRepository;
        _logger = logger;
    }

    public async Task<ChannelConfiguration?> GetChannelConfigAsync(
        string tenantId,
        long channelId,
        CancellationToken ct = default)
    {
        var key = GetChannelCacheKey(tenantId, channelId);

        if (_channelCache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            return cached.Config;
        }

        // Cache miss - fetch from repository
        var config = await _channelRepository.GetByTenantAndChannelAsync(tenantId, channelId, ct);

        // Only cache enabled configurations
        if (config?.IsEnabled == true)
        {
            _channelCache[key] = new CachedChannelConfig(config);
            _logger.LogDebug("Cached channel config for tenant {TenantId}, channel {ChannelId}",
                tenantId, channelId);
        }

        return config?.IsEnabled == true ? config : null;
    }

    public async Task<TradingConfiguration?> GetTradingConfigAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        if (_tradingCache.TryGetValue(tenantId, out var cached) && !cached.IsExpired)
        {
            return cached.Config;
        }

        // Cache miss - fetch from repository
        var config = await _tradingRepository.GetByTenantIdAsync(tenantId, ct);

        if (config != null)
        {
            _tradingCache[tenantId] = new CachedTradingConfig(config);
            _logger.LogDebug("Cached trading config for tenant {TenantId}", tenantId);
        }

        return config;
    }

    public async Task<Tenant?> GetTenantAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        if (_tenantCache.TryGetValue(tenantId, out var cached) && !cached.IsExpired)
        {
            return cached.Tenant;
        }

        // Cache miss - fetch from repository
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, ct);

        // Only cache active tenants
        if (tenant?.Status == TenantStatus.Active)
        {
            _tenantCache[tenantId] = new CachedTenant(tenant);
            _logger.LogDebug("Cached tenant {TenantId}", tenantId);
        }

        return tenant?.Status == TenantStatus.Active ? tenant : null;
    }

    public void InvalidateTenant(string tenantId)
    {
        _tenantCache.TryRemove(tenantId, out _);
        _tradingCache.TryRemove(tenantId, out _);

        // Remove all channel configs for this tenant
        var keysToRemove = _channelCache.Keys
            .Where(k => k.StartsWith($"{tenantId}:"))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _channelCache.TryRemove(key, out _);
        }

        _logger.LogInformation("Invalidated cache for tenant {TenantId}", tenantId);
    }

    public void InvalidateAll()
    {
        _tenantCache.Clear();
        _channelCache.Clear();
        _tradingCache.Clear();

        _logger.LogInformation("Invalidated all caches");
    }

    private static string GetChannelCacheKey(string tenantId, long channelId)
        => $"{tenantId}:{channelId}";

    private sealed record CachedTenant(Tenant Tenant)
    {
        public DateTimeOffset CachedAt { get; } = DateTimeOffset.UtcNow;
        public bool IsExpired => DateTimeOffset.UtcNow - CachedAt > CacheTtl;
    }

    private sealed record CachedChannelConfig(ChannelConfiguration Config)
    {
        public DateTimeOffset CachedAt { get; } = DateTimeOffset.UtcNow;
        public bool IsExpired => DateTimeOffset.UtcNow - CachedAt > CacheTtl;
    }

    private sealed record CachedTradingConfig(TradingConfiguration Config)
    {
        public DateTimeOffset CachedAt { get; } = DateTimeOffset.UtcNow;
        public bool IsExpired => DateTimeOffset.UtcNow - CachedAt > CacheTtl;
    }
}