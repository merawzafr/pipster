using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Pipster.Application.Handlers;

/// <summary>
/// In-memory implementation of tenant config provider for development/testing.
/// TODO: Replace with database-backed implementation for production.
/// </summary>
public class InMemoryTenantConfigProvider : ITenantConfigProvider
{
    private readonly ConcurrentDictionary<string, TenantConfig> _configs = new();
    private readonly ILogger<InMemoryTenantConfigProvider> _logger;

    public InMemoryTenantConfigProvider(ILogger<InMemoryTenantConfigProvider> logger)
    {
        _logger = logger;
        SeedDefaultConfigs();
    }

    public Task<TenantConfig?> GetConfigAsync(string tenantId, CancellationToken ct)
    {
        _configs.TryGetValue(tenantId, out var config);
        return Task.FromResult(config);
    }

    /// <summary>
    /// Adds or updates a tenant configuration.
    /// </summary>
    public void UpsertConfig(TenantConfig config)
    {
        _configs[config.TenantId] = config;
        _logger.LogInformation("Updated config for tenant {TenantId}", config.TenantId);
    }

    /// <summary>
    /// Removes a tenant configuration.
    /// </summary>
    public void RemoveConfig(string tenantId)
    {
        _configs.TryRemove(tenantId, out _);
        _logger.LogInformation("Removed config for tenant {TenantId}", tenantId);
    }

    private void SeedDefaultConfigs()
    {
        // Default tenant for testing/development
        var defaultConfig = new TenantConfig
        {
            TenantId = "default",
            RegexPattern = @"(?<side>buy|sell)\s*#?(?<symbol>[A-Z]{6,}).*?(?<entry>\d+\.?\d*)-?\d*\.?\d*.*?sl\s*(?<sl>\d+\.?\d*).*?tp\s*(?<tp1>\d+\.?\d*).*?tp\s*(?<tp2>\d+\.?\d*).*?tp\s*(?<tp3>\d+\.?\d*)",
            ObservedChannels = new List<long>(), // Will be populated when channels are added
            WhitelistedSymbols = new List<string>
            {
                "XAUUSD", "EURUSD", "GBPUSD", "USDJPY", "AUDUSD",
                "USDCAD", "USDCHF", "NZDUSD", "EURGBP", "EURJPY"
            }
        };

        _configs[defaultConfig.TenantId] = defaultConfig;
        _logger.LogInformation("Seeded default tenant configuration");
    }
}