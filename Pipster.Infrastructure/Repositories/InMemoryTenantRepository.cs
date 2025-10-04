using System.Collections.Concurrent;
using Pipster.Domain.Entities;
using Pipster.Domain.Enums;
using Pipster.Domain.Repositories;

namespace Pipster.Infrastructure.Repositories;

/// <summary>
/// In-memory implementation of tenant repository for MVP/testing.
/// Thread-safe using ConcurrentDictionary.
/// CAUTION: Data is lost on application restart - use only for development.
/// </summary>
public sealed class InMemoryTenantRepository : ITenantRepository
{
    private readonly ConcurrentDictionary<string, Tenant> _tenants = new();
    private readonly ConcurrentDictionary<string, string> _emailToIdIndex = new();

    public Task<Tenant?> GetByIdAsync(string tenantId, CancellationToken ct = default)
    {
        _tenants.TryGetValue(tenantId, out var tenant);
        return Task.FromResult(tenant);
    }

    public Task<Tenant?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        if (_emailToIdIndex.TryGetValue(email.ToLowerInvariant(), out var tenantId))
        {
            return GetByIdAsync(tenantId, ct);
        }
        return Task.FromResult<Tenant?>(null);
    }

    public Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default)
    {
        var activeTenants = _tenants.Values
            .Where(t => t.Status == TenantStatus.Active)
            .ToList();
        return Task.FromResult<IReadOnlyList<Tenant>>(activeTenants);
    }

    public Task<IReadOnlyList<Tenant>> GetByStatusAsync(TenantStatus status, CancellationToken ct = default)
    {
        var tenants = _tenants.Values
            .Where(t => t.Status == status)
            .ToList();
        return Task.FromResult<IReadOnlyList<Tenant>>(tenants);
    }

    public Task<IReadOnlyList<Tenant>> GetByChannelIdAsync(string channelId, CancellationToken ct = default)
    {
        var tenants = _tenants.Values
            .Where(t => t.SubscribedChannelIds.Contains(channelId))
            .ToList();
        return Task.FromResult<IReadOnlyList<Tenant>>(tenants);
    }

    public Task AddAsync(Tenant tenant, CancellationToken ct = default)
    {
        if (!_tenants.TryAdd(tenant.Id, tenant))
        {
            throw new InvalidOperationException($"Tenant with ID '{tenant.Id}' already exists");
        }

        _emailToIdIndex.TryAdd(tenant.Email.ToLowerInvariant(), tenant.Id);

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Tenant tenant, CancellationToken ct = default)
    {
        if (!_tenants.ContainsKey(tenant.Id))
        {
            throw new InvalidOperationException($"Tenant '{tenant.Id}' not found");
        }

        _tenants[tenant.Id] = tenant;
        _emailToIdIndex[tenant.Email.ToLowerInvariant()] = tenant.Id;

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string tenantId, CancellationToken ct = default)
    {
        if (_tenants.TryRemove(tenantId, out var tenant))
        {
            _emailToIdIndex.TryRemove(tenant.Email.ToLowerInvariant(), out _);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string tenantId, CancellationToken ct = default)
    {
        return Task.FromResult(_tenants.ContainsKey(tenantId));
    }
}