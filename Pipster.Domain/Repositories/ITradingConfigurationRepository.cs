using Pipster.Domain.Entities;

namespace Pipster.Domain.Repositories;

/// <summary>
/// Repository for trading configuration persistence.
/// Manages risk settings, symbol whitelists, and position sizing per tenant.
/// </summary>
public interface ITradingConfigurationRepository
{
    /// <summary>
    /// Retrieves a trading configuration by ID
    /// </summary>
    Task<TradingConfiguration?> GetByIdAsync(string configId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the trading configuration for a tenant
    /// (typically one config per tenant)
    /// </summary>
    Task<TradingConfiguration?> GetByTenantIdAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all trading configurations
    /// </summary>
    Task<IReadOnlyList<TradingConfiguration>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Adds a new trading configuration
    /// </summary>
    Task AddAsync(TradingConfiguration config, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing trading configuration
    /// </summary>
    Task UpdateAsync(TradingConfiguration config, CancellationToken ct = default);

    /// <summary>
    /// Deletes a trading configuration
    /// </summary>
    Task DeleteAsync(string configId, CancellationToken ct = default);

    /// <summary>
    /// Deletes the trading configuration for a tenant
    /// </summary>
    Task DeleteByTenantIdAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Checks if a trading configuration exists for a tenant
    /// </summary>
    Task<bool> ExistsForTenantAsync(string tenantId, CancellationToken ct = default);
}