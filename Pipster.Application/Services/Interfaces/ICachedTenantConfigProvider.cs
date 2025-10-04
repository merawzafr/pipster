using Pipster.Domain.Entities;

namespace Pipster.Application.Services;

/// <summary>
/// Provides cached access to tenant configurations for high-performance message processing.
/// Caches are invalidated after a configurable TTL (default: 5 minutes).
/// </summary>
public interface ICachedTenantConfigProvider
{
    /// <summary>
    /// Gets channel configuration for a tenant and channel ID.
    /// Returns null if not found or disabled.
    /// </summary>
    Task<ChannelConfiguration?> GetChannelConfigAsync(
        string tenantId,
        long channelId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets trading configuration for a tenant.
    /// Returns null if not found.
    /// </summary>
    Task<TradingConfiguration?> GetTradingConfigAsync(
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets tenant information.
    /// Returns null if not found or inactive.
    /// </summary>
    Task<Tenant?> GetTenantAsync(
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidates cache for a specific tenant.
    /// Call this after updating tenant configurations.
    /// </summary>
    void InvalidateTenant(string tenantId);

    /// <summary>
    /// Invalidates all caches.
    /// </summary>
    void InvalidateAll();
}