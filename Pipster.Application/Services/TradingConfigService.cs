using Microsoft.Extensions.Logging;
using Pipster.Domain.Entities;
using Pipster.Domain.Repositories;

namespace Pipster.Application.Services;

/// <summary>
/// Implementation of trading configuration application service.
/// Manages risk settings, symbol whitelists, and position sizing.
/// </summary>
public sealed class TradingConfigService : ITradingConfigService
{
    private readonly ITradingConfigurationRepository _configRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<TradingConfigService> _logger;

    public TradingConfigService(
        ITradingConfigurationRepository configRepository,
        ITenantRepository tenantRepository,
        ILogger<TradingConfigService> logger)
    {
        _configRepository = configRepository;
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    public async Task<TradingConfiguration> CreateDefaultConfigAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        // Validate tenant exists
        await ValidateTenantExistsAsync(tenantId, ct);

        // Check if config already exists
        var existing = await _configRepository.GetByTenantIdAsync(tenantId, ct);
        if (existing != null)
        {
            throw new InvalidOperationException(
                $"Trading configuration already exists for tenant {tenantId}");
        }

        // Create default configuration using domain factory
        var config = TradingConfiguration.CreateDefault(tenantId);

        await _configRepository.AddAsync(config, ct);

        _logger.LogInformation("Created default trading configuration for tenant {TenantId}", tenantId);

        return config;
    }

    public async Task<TradingConfiguration?> GetConfigAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        return await _configRepository.GetByTenantIdAsync(tenantId, ct);
    }

    public async Task SetFixedSizingAsync(
        string tenantId,
        int units,
        CancellationToken ct = default)
    {
        var config = await GetConfigOrThrowAsync(tenantId, ct);

        config.SetFixedSizing(units);

        await _configRepository.UpdateAsync(config, ct);

        _logger.LogInformation(
            "Set fixed sizing to {Units} units for tenant {TenantId}",
            units, tenantId);
    }

    public async Task SetPercentageSizingAsync(
        string tenantId,
        decimal equityPercent,
        CancellationToken ct = default)
    {
        var config = await GetConfigOrThrowAsync(tenantId, ct);

        config.SetPercentageSizing(equityPercent);

        await _configRepository.UpdateAsync(config, ct);

        _logger.LogInformation(
            "Set percentage sizing to {Percent}% for tenant {TenantId}",
            equityPercent, tenantId);
    }

    public async Task SetMaxExposureAsync(
        string tenantId,
        decimal maxExposurePercent,
        CancellationToken ct = default)
    {
        var config = await GetConfigOrThrowAsync(tenantId, ct);

        config.SetMaxExposure(maxExposurePercent);

        await _configRepository.UpdateAsync(config, ct);

        _logger.LogInformation(
            "Set max exposure to {Percent}% for tenant {TenantId}",
            maxExposurePercent, tenantId);
    }

    public async Task WhitelistSymbolAsync(
        string tenantId,
        string symbol,
        CancellationToken ct = default)
    {
        var config = await GetConfigOrThrowAsync(tenantId, ct);

        config.WhitelistSymbol(symbol);

        await _configRepository.UpdateAsync(config, ct);

        _logger.LogInformation(
            "Whitelisted symbol {Symbol} for tenant {TenantId}",
            symbol, tenantId);
    }

    public async Task RemoveFromWhitelistAsync(
        string tenantId,
        string symbol,
        CancellationToken ct = default)
    {
        var config = await GetConfigOrThrowAsync(tenantId, ct);

        config.RemoveFromWhitelist(symbol);

        await _configRepository.UpdateAsync(config, ct);

        _logger.LogInformation(
            "Removed symbol {Symbol} from whitelist for tenant {TenantId}",
            symbol, tenantId);
    }

    public async Task BlacklistSymbolAsync(
        string tenantId,
        string symbol,
        CancellationToken ct = default)
    {
        var config = await GetConfigOrThrowAsync(tenantId, ct);

        config.BlacklistSymbol(symbol);

        await _configRepository.UpdateAsync(config, ct);

        _logger.LogInformation(
            "Blacklisted symbol {Symbol} for tenant {TenantId}",
            symbol, tenantId);
    }

    public async Task RemoveFromBlacklistAsync(
        string tenantId,
        string symbol,
        CancellationToken ct = default)
    {
        var config = await GetConfigOrThrowAsync(tenantId, ct);

        config.RemoveFromBlacklist(symbol);

        await _configRepository.UpdateAsync(config, ct);

        _logger.LogInformation(
            "Removed symbol {Symbol} from blacklist for tenant {TenantId}",
            symbol, tenantId);
    }

    public async Task SetTradingSessionAsync(
        string tenantId,
        TimeOnly startUtc,
        TimeOnly endUtc,
        DayOfWeek[]? allowedDays = null,
        CancellationToken ct = default)
    {
        var config = await GetConfigOrThrowAsync(tenantId, ct);

        config.SetTradingSession(startUtc, endUtc, allowedDays);

        await _configRepository.UpdateAsync(config, ct);

        _logger.LogInformation(
            "Set trading session {Start}-{End} UTC for tenant {TenantId}",
            startUtc, endUtc, tenantId);
    }

    public async Task ClearTradingSessionAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var config = await GetConfigOrThrowAsync(tenantId, ct);

        config.ClearTradingSession();

        await _configRepository.UpdateAsync(config, ct);

        _logger.LogInformation(
            "Cleared trading session restrictions for tenant {TenantId}",
            tenantId);
    }

    public async Task EnableAutoExecutionAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var config = await GetConfigOrThrowAsync(tenantId, ct);

        config.EnableAutoExecution();

        await _configRepository.UpdateAsync(config, ct);

        _logger.LogInformation(
            "Enabled auto-execution for tenant {TenantId}",
            tenantId);
    }

    public async Task DisableAutoExecutionAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var config = await GetConfigOrThrowAsync(tenantId, ct);

        config.DisableAutoExecution();

        await _configRepository.UpdateAsync(config, ct);

        _logger.LogInformation(
            "Disabled auto-execution for tenant {TenantId}",
            tenantId);
    }

    public async Task SetMaxSlippageAsync(
        string tenantId,
        decimal pips,
        CancellationToken ct = default)
    {
        var config = await GetConfigOrThrowAsync(tenantId, ct);

        config.SetMaxSlippage(pips);

        await _configRepository.UpdateAsync(config, ct);

        _logger.LogInformation(
            "Set max slippage to {Pips} pips for tenant {TenantId}",
            pips, tenantId);
    }

    public async Task<bool> IsSymbolAllowedAsync(
        string tenantId,
        string symbol,
        CancellationToken ct = default)
    {
        var config = await GetConfigOrThrowAsync(tenantId, ct);

        return config.IsSymbolAllowed(symbol);
    }

    public async Task<bool> IsTradingAllowedAtAsync(
        string tenantId,
        DateTimeOffset timestamp,
        CancellationToken ct = default)
    {
        var config = await GetConfigOrThrowAsync(tenantId, ct);

        return config.IsTradingAllowedNow(timestamp);
    }

    private async Task<TradingConfiguration> GetConfigOrThrowAsync(
        string tenantId,
        CancellationToken ct)
    {
        var config = await _configRepository.GetByTenantIdAsync(tenantId, ct);
        if (config == null)
        {
            throw new InvalidOperationException(
                $"Trading configuration not found for tenant {tenantId}");
        }
        return config;
    }

    private async Task ValidateTenantExistsAsync(string tenantId, CancellationToken ct)
    {
        var exists = await _tenantRepository.ExistsAsync(tenantId, ct);
        if (!exists)
        {
            throw new InvalidOperationException($"Tenant '{tenantId}' not found");
        }
    }
}