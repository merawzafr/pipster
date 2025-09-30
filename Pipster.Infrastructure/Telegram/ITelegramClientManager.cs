using Pipster.Shared.Contracts.Telegram;

namespace Pipster.Infrastructure.Telegram;

/// <summary>
/// Manages lifecycle of TDLib clients for multiple tenants.
/// Handles connection pooling, session management, and graceful shutdown.
/// </summary>
public interface ITelegramClientManager
{
    Task<ITelegramClient> GetOrCreateClientAsync(string tenantId, TelegramCredentials credentials, CancellationToken ct);
    Task RemoveClientAsync(string tenantId, CancellationToken ct);
    Task<bool> IsClientActiveAsync(string tenantId);
    Task ShutdownAllAsync(CancellationToken ct);
}