using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Pipster.Shared.Contracts.Telegram;

namespace Pipster.Infrastructure.Telegram;

/// <summary>
/// Manages lifecycle of TDLib clients for multiple tenants.
/// Handles connection pooling, session management, and graceful shutdown.
/// </summary>
public class TelegramClientManager : ITelegramClientManager, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, TelegramClientWrapper> _clients = new();
    private readonly ITelegramSessionStore _sessionStore;
    private readonly ILogger<TelegramClientManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly TelegramClientOptions _options;
    private readonly SemaphoreSlim _creationLock = new(1, 1);

    public TelegramClientManager(
        ITelegramSessionStore sessionStore,
        ILogger<TelegramClientManager> logger,
        ILoggerFactory loggerFactory,
        TelegramClientOptions options)
    {
        _sessionStore = sessionStore;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _options = options;
    }

    public async Task<ITelegramClient> GetOrCreateClientAsync(
        string tenantId,
        TelegramCredentials credentials,
        CancellationToken ct)
    {
        if (_clients.TryGetValue(tenantId, out var wrapper))
        {
            if (wrapper.IsHealthy)
            {
                wrapper.UpdateLastAccess();
                return wrapper.Client;
            }

            // Client unhealthy, remove and recreate
            await RemoveClientAsync(tenantId, ct);
        }

        await _creationLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_clients.TryGetValue(tenantId, out wrapper))
            {
                wrapper.UpdateLastAccess();
                return wrapper.Client;
            }

            var client = await CreateClientAsync(tenantId, credentials, ct);
            var newWrapper = new TelegramClientWrapper(client, tenantId);

            if (!_clients.TryAdd(tenantId, newWrapper))
            {
                await client.DisposeAsync();
                throw new InvalidOperationException($"Failed to add client for tenant {tenantId}");
            }

            _logger.LogInformation("Created Telegram client for tenant {TenantId}", tenantId);
            return client;
        }
        finally
        {
            _creationLock.Release();
        }
    }

    public async Task RemoveClientAsync(string tenantId, CancellationToken ct)
    {
        if (_clients.TryRemove(tenantId, out var wrapper))
        {
            await wrapper.DisposeAsync();
            _logger.LogInformation("Removed Telegram client for tenant {TenantId}", tenantId);
        }
    }

    public Task<bool> IsClientActiveAsync(string tenantId)
    {
        return Task.FromResult(_clients.TryGetValue(tenantId, out var wrapper) && wrapper.IsHealthy);
    }

    public async Task ShutdownAllAsync(CancellationToken ct)
    {
        _logger.LogInformation("Shutting down all Telegram clients ({Count})", _clients.Count);

        var shutdownTasks = _clients.Values.Select(w => w.DisposeAsync().AsTask());
        await Task.WhenAll(shutdownTasks);

        _clients.Clear();
    }

    private async Task<ITelegramClient> CreateClientAsync(
        string tenantId,
        TelegramCredentials credentials,
        CancellationToken ct)
    {
        var sessionPath = await _sessionStore.GetSessionPathAsync(tenantId, ct);

        var clientLogger = _loggerFactory.CreateLogger<ResilientTelegramClient>();

        var client = new ResilientTelegramClient(
            tenantId,
            credentials,
            sessionPath,
            _options,
            clientLogger);

        await client.ConnectAsync(ct);
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        await ShutdownAllAsync(CancellationToken.None);
        _creationLock.Dispose();
    }

    private class TelegramClientWrapper : IAsyncDisposable
    {
        public ITelegramClient Client { get; }
        public string TenantId { get; }
        public DateTimeOffset LastAccess { get; private set; }
        public bool IsHealthy => Client.IsConnected && !_disposed;

        private bool _disposed;

        public TelegramClientWrapper(ITelegramClient client, string tenantId)
        {
            Client = client;
            TenantId = tenantId;
            LastAccess = DateTimeOffset.UtcNow;
        }

        public void UpdateLastAccess() => LastAccess = DateTimeOffset.UtcNow;

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            await Client.DisposeAsync();
        }
    }
}