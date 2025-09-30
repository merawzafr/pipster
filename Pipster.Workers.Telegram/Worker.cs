using Pipster.Infrastructure.Messaging;
using Pipster.Infrastructure.Telegram;
using Pipster.Shared.Contracts.Telegram;

namespace Pipster.Workers.Telegram;

public class TelegramWorkerService : BackgroundService
{
    private readonly ITelegramClientManager _clientManager;
    private readonly IMessageBus _bus;
    private readonly ILogger<TelegramWorkerService> _logger;
    private readonly TelegramClientOptions _options;
    private readonly PeriodicTimer _healthCheckTimer;

    public TelegramWorkerService(
        ITelegramClientManager clientManager,
        IMessageBus bus,
        ILogger<TelegramWorkerService> logger,
        TelegramClientOptions options)
    {
        _clientManager = clientManager;
        _bus = bus;
        _logger = logger;
        _options = options;
        _healthCheckTimer = new PeriodicTimer(options.HealthCheckInterval);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telegram Worker Service starting");

        // Start background tasks
        var addChannelTask = ConsumeAddChannelRequestsAsync(stoppingToken);
        var removeChannelTask = ConsumeRemoveChannelRequestsAsync(stoppingToken);
        var healthCheckTask = RunHealthChecksAsync(stoppingToken);

        _logger.LogInformation("Telegram Worker Service started and ready to accept connections");

        await Task.WhenAll(addChannelTask, removeChannelTask, healthCheckTask);
    }

    private async Task ConsumeAddChannelRequestsAsync(CancellationToken ct)
    {
        await foreach (var request in _bus.ConsumeChannelRequestsAsync(ct))
        {
            try
            {
                _logger.LogInformation("Adding channel {ChannelId} for tenant {TenantId}",
                    request.ChannelId, request.TenantId);

                // TODO: Get tenant credentials from database
                var credentials = new TelegramCredentials
                {
                    ApiId = 0, // TODO: Load from configuration
                    ApiHash = string.Empty // TODO: Load from configuration
                };

                var client = await _clientManager.GetOrCreateClientAsync(
                    request.TenantId,
                    credentials,
                    ct);

                await client.AddChannelAsync(request.ChannelId, ct);

                _logger.LogInformation("Successfully added channel {ChannelId} for tenant {TenantId}",
                    request.ChannelId, request.TenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding channel {ChannelId} for tenant {TenantId}",
                    request.ChannelId, request.TenantId);
            }
        }
    }

    private async Task ConsumeRemoveChannelRequestsAsync(CancellationToken ct)
    {
        await foreach (var request in _bus.ConsumeRemoveChannelRequestsAsync(ct))
        {
            try
            {
                _logger.LogInformation("Removing channel {ChannelId} for tenant {TenantId}",
                    request.ChannelId, request.TenantId);

                if (await _clientManager.IsClientActiveAsync(request.TenantId))
                {
                    // TODO: Get tenant credentials from database
                    var credentials = new TelegramCredentials
                    {
                        ApiId = 0,
                        ApiHash = string.Empty
                    };

                    var client = await _clientManager.GetOrCreateClientAsync(
                        request.TenantId,
                        credentials,
                        ct);

                    await client.RemoveChannelAsync(request.ChannelId, ct);
                }

                _logger.LogInformation("Successfully removed channel {ChannelId} for tenant {TenantId}",
                    request.ChannelId, request.TenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing channel {ChannelId} for tenant {TenantId}",
                    request.ChannelId, request.TenantId);
            }
        }
    }

    private async Task RunHealthChecksAsync(CancellationToken ct)
    {
        while (await _healthCheckTimer.WaitForNextTickAsync(ct))
        {
            try
            {
                // TODO: Implement health checks
                _logger.LogDebug("Health check completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Telegram Worker Service stopping");

        _healthCheckTimer.Dispose();

        await _clientManager.ShutdownAllAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}