using Pipster.Domain.Entities;
using Pipster.Domain.Enums;

namespace Pipster.Application.Services;

/// <summary>
/// Application service for tenant management operations.
/// Orchestrates domain logic and repository interactions.
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Creates a new tenant
    /// </summary>
    Task<Tenant> CreateTenantAsync(
        string id,
        string email,
        string displayName,
        SubscriptionPlan plan,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a tenant by ID
    /// </summary>
    Task<Tenant?> GetTenantAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a tenant by email
    /// </summary>
    Task<Tenant?> GetTenantByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all active tenants (for worker processing)
    /// </summary>
    Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets Telegram credentials for a tenant
    /// </summary>
    Task SetTelegramCredentialsAsync(
        string tenantId,
        int apiId,
        string apiHash,
        string? phoneNumber = null,
        CancellationToken ct = default);

    /// <summary>
    /// Deactivates a tenant (soft delete)
    /// </summary>
    Task DeactivateTenantAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Reactivates a previously deactivated tenant
    /// </summary>
    Task ReactivateTenantAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Changes a tenant's subscription plan
    /// </summary>
    Task ChangePlanAsync(
        string tenantId,
        SubscriptionPlan newPlan,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a tenant exists
    /// </summary>
    Task<bool> TenantExistsAsync(string tenantId, CancellationToken ct = default);
}