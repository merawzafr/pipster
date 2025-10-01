using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pipster.Application.Parsing;
using Pipster.Infrastructure.Idempotency;
using Pipster.Infrastructure.Messaging;
using Pipster.Shared.Contracts;

namespace Pipster.Application.Handlers;

/// <summary>
/// Worker that consumes Telegram messages, parses trading signals, and publishes normalized signals.
/// </summary>
public class TelegramMessageHandlerWorker : BackgroundService
{
    private readonly IMessageBus _bus;
    private readonly ISignalParser _parser;
    private readonly ITenantConfigProvider _configProvider;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<TelegramMessageHandlerWorker> _logger;

    // 24 hour TTL for idempotency keys
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    public TelegramMessageHandlerWorker(
        IMessageBus bus,
        ISignalParser parser,
        ITenantConfigProvider configProvider,
        IIdempotencyStore idempotencyStore,
        ILogger<TelegramMessageHandlerWorker> logger)
    {
        _bus = bus;
        _parser = parser;
        _configProvider = configProvider;
        _idempotencyStore = idempotencyStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TelegramMessageHandler starting");

        await foreach (var message in _bus.ConsumeTelegramMessagesAsync(stoppingToken))
        {
            try
            {
                await ProcessMessageAsync(message, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing message {MessageKey} for tenant {TenantId}",
                    message.MessageKey, message.TenantId);
            }
        }
    }

    private async Task ProcessMessageAsync(
        Shared.Contracts.Telegram.TelegramMessageReceived message,
        CancellationToken ct)
    {
        _logger.LogDebug(
            "Processing message {MessageId} from channel {ChannelId} for tenant {TenantId}",
            message.MessageId, message.ChannelId, message.TenantId);

        // Step 1: Get tenant configuration
        var config = await _configProvider.GetConfigAsync(message.TenantId, ct);
        if (config == null)
        {
            _logger.LogWarning(
                "No configuration found for tenant {TenantId}, skipping message {MessageKey}",
                message.TenantId, message.MessageKey);
            return;
        }

        // Step 2: Validate channel is in tenant's whitelist
        if (!config.ObservedChannels.Contains(message.ChannelId))
        {
            _logger.LogDebug(
                "Channel {ChannelId} not in whitelist for tenant {TenantId}, skipping",
                message.ChannelId, message.TenantId);
            return;
        }

        // Step 3: Parse the signal using tenant's regex pattern
        var signal = _parser.TryParse(config.RegexPattern, message.Content);
        if (signal == null)
        {
            _logger.LogDebug(
                "Failed to parse signal from message {MessageKey} for tenant {TenantId}. Content: {Content}",
                message.MessageKey, message.TenantId, message.Content);
            return;
        }

        // Step 4: Apply tenant and channel context to the signal
        var enrichedSignal = signal with
        {
            TenantId = message.TenantId,
            Source = $"telegram:{message.ChannelId}",
            SeenAt = message.Timestamp
        };

        // Step 5: Validate against tenant's rules
        if (!ValidateSignal(enrichedSignal, config))
        {
            _logger.LogWarning(
                "Signal validation failed for {Symbol} from tenant {TenantId}",
                enrichedSignal.Symbol, message.TenantId);
            return;
        }

        // Step 6: Publish normalized signal
        await _bus.PublishSignalAsync(enrichedSignal, ct);

        _logger.LogInformation(
            "Published signal: {Symbol} {Side} for tenant {TenantId} from message {MessageKey}",
            enrichedSignal.Symbol, enrichedSignal.Side, message.TenantId, message.MessageKey);
    }

    private bool ValidateSignal(NormalizedSignal signal, TenantConfig config)
    {
        // Validate symbol whitelist
        if (config.WhitelistedSymbols.Count > 0 &&
            !config.WhitelistedSymbols.Contains(signal.Symbol))
        {
            _logger.LogDebug(
                "Symbol {Symbol} not in whitelist for tenant {TenantId}",
                signal.Symbol, config.TenantId);
            return false;
        }

        // Additional validation rules can be added here:
        // - Trading session hours
        // - Maximum positions
        // - Symbol-specific rules
        // - Risk limits

        return true;
    }
}