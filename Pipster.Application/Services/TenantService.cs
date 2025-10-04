using Microsoft.Extensions.Logging;
using Pipster.Domain.Entities;
using Pipster.Domain.Enums;
using Pipster.Domain.Repositories;

namespace Pipster.Application.Services;

/// <summary>
/// Implementation of tenant management application service.
/// Orchestrates tenant operations with domain validation.
/// </summary>
public sealed class TenantService : ITenantService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<TenantService> _logger;

    public TenantService(
        ITenantRepository tenantRepository,
        ILogger<TenantService> logger)
    {
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    public async Task<Tenant> CreateTenantAsync(
        string id,
        string email,
        string displayName,
        SubscriptionPlan plan,
        CancellationToken ct = default)
    {
        // Check if tenant already exists
        if (await _tenantRepository.ExistsAsync(id, ct))
        {
            throw new InvalidOperationException($"Tenant with ID '{id}' already exists");
        }

        // Check if email is already registered
        var existingTenant = await _tenantRepository.GetByEmailAsync(email, ct);
        if (existingTenant != null)
        {
            throw new InvalidOperationException($"Email '{email}' is already registered");
        }

        // Create tenant using domain factory method
        var tenant = Tenant.Create(id, email, displayName, plan);

        await _tenantRepository.AddAsync(tenant, ct);

        _logger.LogInformation("Created tenant {TenantId} with email {Email} and plan {Plan}",
            tenant.Id, tenant.Email, tenant.Plan);

        return tenant;
    }

    public async Task<Tenant?> GetTenantAsync(string tenantId, CancellationToken ct = default)
    {
        return await _tenantRepository.GetByIdAsync(tenantId, ct);
    }

    public async Task<Tenant?> GetTenantByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _tenantRepository.GetByEmailAsync(email, ct);
    }

    public async Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default)
    {
        return await _tenantRepository.GetActiveTenantsAsync(ct);
    }

    public async Task SetTelegramCredentialsAsync(
        string tenantId,
        int apiId,
        string apiHash,
        string? phoneNumber = null,
        CancellationToken ct = default)
    {
        var tenant = await GetTenantOrThrowAsync(tenantId, ct);

        // Domain method handles validation
        tenant.SetTelegramCredentials(apiId, apiHash, phoneNumber);

        await _tenantRepository.UpdateAsync(tenant, ct);

        _logger.LogInformation("Updated Telegram credentials for tenant {TenantId}", tenantId);
    }

    public async Task DeactivateTenantAsync(string tenantId, CancellationToken ct = default)
    {
        var tenant = await GetTenantOrThrowAsync(tenantId, ct);

        tenant.Deactivate();

        await _tenantRepository.UpdateAsync(tenant, ct);

        _logger.LogInformation("Deactivated tenant {TenantId}", tenantId);
    }

    public async Task ReactivateTenantAsync(string tenantId, CancellationToken ct = default)
    {
        var tenant = await GetTenantOrThrowAsync(tenantId, ct);

        tenant.Reactivate();

        await _tenantRepository.UpdateAsync(tenant, ct);

        _logger.LogInformation("Reactivated tenant {TenantId}", tenantId);
    }

    public async Task ChangePlanAsync(
        string tenantId,
        SubscriptionPlan newPlan,
        CancellationToken ct = default)
    {
        var tenant = await GetTenantOrThrowAsync(tenantId, ct);

        var oldPlan = tenant.Plan;
        tenant.ChangePlan(newPlan);

        await _tenantRepository.UpdateAsync(tenant, ct);

        _logger.LogInformation("Changed plan for tenant {TenantId} from {OldPlan} to {NewPlan}",
            tenantId, oldPlan, newPlan);
    }

    public async Task<bool> TenantExistsAsync(string tenantId, CancellationToken ct = default)
    {
        return await _tenantRepository.ExistsAsync(tenantId, ct);
    }

    private async Task<Tenant> GetTenantOrThrowAsync(string tenantId, CancellationToken ct)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, ct);
        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant '{tenantId}' not found");
        }
        return tenant;
    }
}