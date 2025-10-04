using Pipster.Domain.Entities;
using Pipster.Domain.Enums;

namespace Pipster.Domain.Repositories;

/// <summary>
/// Repository for broker connection persistence
/// </summary>
public interface IBrokerConnectionRepository
{
    /// <summary>
    /// Retrieves a broker connection by ID
    /// </summary>
    Task<BrokerConnection?> GetByIdAsync(string connectionId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all broker connections for a tenant
    /// </summary>
    Task<IReadOnlyList<BrokerConnection>> GetByTenantIdAsync(
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves active broker connections for a tenant
    /// </summary>
    Task<IReadOnlyList<BrokerConnection>> GetActiveByTenantIdAsync(
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves broker connections by tenant and broker type
    /// </summary>
    Task<IReadOnlyList<BrokerConnection>> GetByTenantAndTypeAsync(
        string tenantId,
        BrokerType brokerType,
        CancellationToken ct = default);

    /// <summary>
    /// Adds a new broker connection
    /// </summary>
    Task AddAsync(BrokerConnection connection, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing broker connection
    /// </summary>
    Task UpdateAsync(BrokerConnection connection, CancellationToken ct = default);

    /// <summary>
    /// Deletes a broker connection
    /// </summary>
    Task DeleteAsync(string connectionId, CancellationToken ct = default);

    /// <summary>
    /// Checks if a broker connection exists
    /// </summary>
    Task<bool> ExistsAsync(string connectionId, CancellationToken ct = default);
}