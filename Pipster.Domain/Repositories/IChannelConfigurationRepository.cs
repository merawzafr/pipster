using Pipster.Domain.Entities;

namespace Pipster.Domain.Repositories;

/// <summary>
/// Repository for channel configuration persistence.
/// Manages Telegram channel monitoring settings per tenant.
/// </summary>
public interface IChannelConfigurationRepository
{
    /// <summary>
    /// Retrieves a channel configuration by ID
    /// </summary>
    Task<ChannelConfiguration?> GetByIdAsync(string configId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a channel configuration by tenant and channel ID
    /// </summary>
    Task<ChannelConfiguration?> GetByTenantAndChannelAsync(
        string tenantId,
        long channelId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves all channel configurations for a tenant
    /// </summary>
    Task<IReadOnlyList<ChannelConfiguration>> GetByTenantIdAsync(
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves all enabled channel configurations for a tenant
    /// </summary>
    Task<IReadOnlyList<ChannelConfiguration>> GetEnabledByTenantIdAsync(
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves all tenants monitoring a specific channel
    /// </summary>
    Task<IReadOnlyList<ChannelConfiguration>> GetByChannelIdAsync(
        long channelId,
        CancellationToken ct = default);

    /// <summary>
    /// Adds a new channel configuration
    /// </summary>
    Task AddAsync(ChannelConfiguration config, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing channel configuration
    /// </summary>
    Task UpdateAsync(ChannelConfiguration config, CancellationToken ct = default);

    /// <summary>
    /// Deletes a channel configuration
    /// </summary>
    Task DeleteAsync(string configId, CancellationToken ct = default);

    /// <summary>
    /// Deletes all channel configurations for a tenant
    /// </summary>
    Task DeleteByTenantIdAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Checks if a channel configuration exists
    /// </summary>
    Task<bool> ExistsAsync(string tenantId, long channelId, CancellationToken ct = default);
}