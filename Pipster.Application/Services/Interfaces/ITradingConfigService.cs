using Pipster.Domain.Entities;

namespace Pipster.Application.Services;

/// <summary>
/// Application service for managing trading configurations.
/// Handles risk settings, symbol whitelists, position sizing, and trading sessions.
/// </summary>
public interface ITradingConfigService
{
    /// <summary>
    /// Creates default trading configuration for a tenant
    /// </summary>
    Task<TradingConfiguration> CreateDefaultConfigAsync(
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves trading configuration for a tenant
    /// </summary>
    Task<TradingConfiguration?> GetConfigAsync(
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Sets fixed position sizing
    /// </summary>
    Task SetFixedSizingAsync(
        string tenantId,
        int units,
        CancellationToken ct = default);

    /// <summary>
    /// Sets percentage-based position sizing
    /// </summary>
    Task SetPercentageSizingAsync(
        string tenantId,
        decimal equityPercent,
        CancellationToken ct = default);

    /// <summary>
    /// Sets maximum total exposure limit
    /// </summary>
    Task SetMaxExposureAsync(
        string tenantId,
        decimal maxExposurePercent,
        CancellationToken ct = default);

    /// <summary>
    /// Adds a symbol to the whitelist
    /// </summary>
    Task WhitelistSymbolAsync(
        string tenantId,
        string symbol,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a symbol from the whitelist
    /// </summary>
    Task RemoveFromWhitelistAsync(
        string tenantId,
        string symbol,
        CancellationToken ct = default);

    /// <summary>
    /// Adds a symbol to the blacklist
    /// </summary>
    Task BlacklistSymbolAsync(
        string tenantId,
        string symbol,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a symbol from the blacklist
    /// </summary>
    Task RemoveFromBlacklistAsync(
        string tenantId,
        string symbol,
        CancellationToken ct = default);

    /// <summary>
    /// Sets trading session hours (UTC)
    /// </summary>
    Task SetTradingSessionAsync(
        string tenantId,
        TimeOnly startUtc,
        TimeOnly endUtc,
        DayOfWeek[]? allowedDays = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes trading session restrictions
    /// </summary>
    Task ClearTradingSessionAsync(
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Enables auto-execution of signals
    /// </summary>
    Task EnableAutoExecutionAsync(
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Disables auto-execution (signals will only be logged)
    /// </summary>
    Task DisableAutoExecutionAsync(
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Sets maximum slippage tolerance
    /// </summary>
    Task SetMaxSlippageAsync(
        string tenantId,
        decimal pips,
        CancellationToken ct = default);

    /// <summary>
    /// Validates if a symbol is allowed for trading
    /// </summary>
    Task<bool> IsSymbolAllowedAsync(
        string tenantId,
        string symbol,
        CancellationToken ct = default);

    /// <summary>
    /// Validates if trading is allowed at a specific time
    /// </summary>
    Task<bool> IsTradingAllowedAtAsync(
        string tenantId,
        DateTimeOffset timestamp,
        CancellationToken ct = default);
}