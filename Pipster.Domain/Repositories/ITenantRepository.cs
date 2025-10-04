using Pipster.Domain.Entities;
using Pipster.Domain.Enums;

namespace Pipster.Domain.Repositories;

/// <summary>
/// Repository for tenant aggregate persistence.
/// Interface resides in Domain, implementation in Infrastructure (dependency inversion).
/// </summary>
public interface ITenantRepository
{
    /// <summary>
    /// Retrieves a tenant by ID
    /// </summary>
    Task<Tenant?> GetByIdAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a tenant by email address
    /// </summary>
    Task<Tenant?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all active tenants
    /// </summary>
    Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves tenants by status
    /// </summary>
    Task<IReadOnlyList<Tenant>> GetByStatusAsync(TenantStatus status, CancellationToken ct = default);

    /// <summary>
    /// Retrieves tenants subscribed to a specific channel
    /// </summary>
    Task<IReadOnlyList<Tenant>> GetByChannelIdAsync(string channelId, CancellationToken ct = default);

    /// <summary>
    /// Adds a new tenant
    /// </summary>
    Task AddAsync(Tenant tenant, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing tenant
    /// </summary>
    Task UpdateAsync(Tenant tenant, CancellationToken ct = default);

    /// <summary>
    /// Deletes a tenant (hard delete - use with caution)
    /// </summary>
    Task DeleteAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Checks if a tenant exists
    /// </summary>
    Task<bool> ExistsAsync(string tenantId, CancellationToken ct = default);
}