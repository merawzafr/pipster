using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pipster.Application.Parsing;
using Pipster.Application.Services;
using Pipster.Infrastructure.Idempotency;
using Pipster.Infrastructure.Messaging;
using Pipster.Shared.Contracts;

namespace Pipster.Workers.Telegram;

/// <summary>
/// Background worker that processes Telegram messages.
/// Pipeline: Idempotency → Tenant Validation → Channel Config → Parse → Risk Check → Publish Trade
/// </summary>
public sealed class TelegramMessageHandlerWorker : BackgroundService
{
    private readonly IMessageBus _messageBus;
    private readonly ISignalParser _signalParser;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ICachedTenantConfigProvider _configProvider;
    private readonly ILogger<TelegramMessageHandlerWorker> _logger;

    public TelegramMessageHandlerWorker(
        IMessageBus messageBus,
        ISignalParser signalParser,
        IIdempotencyStore idempotencyStore,
        ICachedTenantConfigProvider configProvider,
        ILogger<TelegramMessageHandlerWorker> logger)
    {
        _messageBus = messageBus;
        _signalParser = signalParser;
        _idempotencyStore = idempotencyStore;
        _configProvider = configProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telegram Message Handler Worker starting");

        await foreach (var message in _messageBus.ConsumeTelegramMessagesAsync(stoppingToken))
        {
            try
            {
                await ProcessMessageAsync(message, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing Telegram message {MessageKey}",
                    message.MessageKey);
            }
        }
    }

    private async Task ProcessMessageAsync(
        Shared.Contracts.Telegram.TelegramMessageReceived message,
        CancellationToken ct)
    {
        // Step 1: Idempotency check
        if (!await _idempotencyStore.TryMarkAsProcessedAsync(message.MessageKey, TimeSpan.FromHours(24), ct))
        {
            _logger.LogDebug("Duplicate message {MessageKey}, skipping", message.MessageKey);
            return;
        }

        // Step 2: Validate tenant is active
        var tenant = await _configProvider.GetTenantAsync(message.TenantId, ct);
        if (tenant == null)
        {
            _logger.LogWarning(
                "Tenant {TenantId} not found or inactive, skipping message",
                message.TenantId);
            return;
        }

        // Step 3: Get channel configuration
        var channelConfig = await _configProvider.GetChannelConfigAsync(
            message.TenantId,
            message.ChannelId,
            ct);

        if (channelConfig == null)
        {
            _logger.LogDebug(
                "Channel {ChannelId} not configured or disabled for tenant {TenantId}, skipping",
                message.ChannelId, message.TenantId);
            return;
        }

        // Step 4: Parse signal using channel-specific regex
        var normalizedSignal = _signalParser.TryParse(
            channelConfig.RegexPattern,
            message.Content);

        if (normalizedSignal == null)
        {
            _logger.LogDebug(
                "Failed to parse message from channel {ChannelId} for tenant {TenantId}",
                message.ChannelId, message.TenantId);
            return;
        }

        // Update signal with tenant context
        normalizedSignal = normalizedSignal with
        {
            TenantId = message.TenantId,
            Source = $"telegram:{message.ChannelId}",
            SeenAt = message.Timestamp
        };

        _logger.LogInformation(
            "Parsed signal: {Symbol} {Side} from tenant {TenantId}, channel {ChannelId}",
            normalizedSignal.Symbol, normalizedSignal.Side, message.TenantId, message.ChannelId);

        // Step 5: Get trading configuration for risk checks
        var tradingConfig = await _configProvider.GetTradingConfigAsync(message.TenantId, ct);
        if (tradingConfig == null)
        {
            _logger.LogWarning(
                "Trading configuration not found for tenant {TenantId}, skipping signal",
                message.TenantId);
            return;
        }

        // Step 6: Risk validation
        if (!tradingConfig.IsSymbolAllowed(normalizedSignal.Symbol))
        {
            _logger.LogWarning(
                "Symbol {Symbol} not allowed for tenant {TenantId}, skipping signal",
                normalizedSignal.Symbol, message.TenantId);
            return;
        }

        if (!tradingConfig.IsTradingAllowedNow(normalizedSignal.SeenAt))
        {
            _logger.LogWarning(
                "Trading not allowed at {Time} for tenant {TenantId}, skipping signal",
                normalizedSignal.SeenAt, message.TenantId);
            return;
        }

        if (!tradingConfig.AutoExecuteEnabled)
        {
            _logger.LogInformation(
                "Auto-execution disabled for tenant {TenantId}, logging signal only",
                message.TenantId);
            // TODO: Log to audit store for manual review
            return;
        }

        // Step 7: Get broker connections for this channel
        List<string> brokerConnectionIds;

        if (channelConfig.BrokerConnectionIds.Any())
        {
            // Use channel-specific brokers
            brokerConnectionIds = channelConfig.BrokerConnectionIds.ToList();
            _logger.LogInformation(
                "Using {Count} channel-specific broker(s) for tenant {TenantId}, channel {ChannelId}",
                brokerConnectionIds.Count, message.TenantId, message.ChannelId);
        }
        else
        {
            // Use tenant's default active brokers
            // TODO: This will be implemented when we add the broker service
            // For now, we'll skip if no brokers configured on channel
            _logger.LogWarning(
                "No brokers configured for tenant {TenantId}, channel {ChannelId}. Signal will not be executed.",
                message.TenantId, message.ChannelId);
            return;
        }

        if (!brokerConnectionIds.Any())
        {
            _logger.LogWarning(
                "No active broker connections for tenant {TenantId}, channel {ChannelId}",
                message.TenantId, message.ChannelId);
            return;
        }

        // Step 8: Create trade command for EACH broker
        foreach (var brokerConnectionId in brokerConnectionIds)
        {
            var tradeCommand = ConvertToTradeCommand(
                normalizedSignal,
                tradingConfig,
                brokerConnectionId,
                message.ChannelId.ToString());

            await _messageBus.PublishTradeAsync(tradeCommand, ct);

            _logger.LogInformation(
                "Published trade command to broker {BrokerConnectionId} for {Symbol} {Side}, tenant {TenantId}",
                brokerConnectionId, tradeCommand.Symbol, tradeCommand.Side, message.TenantId);
        }
    }

    private static TradeCommand ConvertToTradeCommand(
    NormalizedSignal signal,
    Domain.Entities.TradingConfiguration tradingConfig,
    string brokerConnectionId,
    string sourceChannelId)
    {
        // Determine position size based on configuration
        int units = tradingConfig.SizingMode switch
        {
            Domain.Enums.PositionSizingMode.Fixed => tradingConfig.FixedUnits ?? 1000,
            Domain.Enums.PositionSizingMode.PercentEquity => 1000, // TODO: Calculate based on equity
            _ => 1000
        };

        // Use first take profit (TP1) for MVP
        decimal? takeProfit = signal.TakeProfits.Count > 0
            ? signal.TakeProfits[0]
            : null;

        return new TradeCommand(
            TenantId: signal.TenantId,
            Symbol: signal.Symbol,
            Side: signal.Side,
            Units: units,
            Price: signal.Entry,
            StopLoss: signal.StopLoss,
            TakeProfit: takeProfit,
            CorrelationId: signal.Hash,
            CreatedAt: DateTimeOffset.UtcNow,
            BrokerConnectionId: brokerConnectionId,
            SourceChannelId: sourceChannelId);
    }
}