using Microsoft.Extensions.Logging;
using Pipster.Connectors.IGMarkets.Models.Positions;
using Pipster.Connectors.IGMarkets.Services;
using Pipster.Shared.Contracts;
using Pipster.Shared.Enums;

namespace Pipster.Connectors.IGMarkets;

/// <summary>
/// IG Markets broker connector implementation
/// </summary>
public sealed class IGMarketsConnector : ITradeConnector
{
    private readonly IIGMarketsApiClient _apiClient;
    private readonly ILogger<IGMarketsConnector> _logger;

    private string? _tenantId;
    private bool _isInitialized;

    public string BrokerType => "IGMarkets";
    public bool IsInitialized => _isInitialized;

    public IGMarketsConnector(
        IIGMarketsApiClient apiClient,
        ILogger<IGMarketsConnector> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public Task InitializeAsync(
        string tenantId,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant ID cannot be empty", nameof(tenantId));

        // Validate required credentials
        if (!credentials.ContainsKey("ApiKey") ||
            !credentials.ContainsKey("Username") ||
            !credentials.ContainsKey("Password"))
        {
            throw new ArgumentException(
                "IG Markets requires ApiKey, Username, and Password credentials");
        }

        _tenantId = tenantId;
        _isInitialized = true;

        _logger.LogInformation(
            "Initialized IG Markets connector for tenant {TenantId}",
            tenantId);

        return Task.CompletedTask;
    }

    public async Task<bool> ValidateConnectionAsync(CancellationToken ct = default)
    {
        if (!_isInitialized)
            return false;

        try
        {
            // Try to get market details for a common instrument
            await _apiClient.GetMarketDetailsAsync("CS.D.EURUSD.CFD.IP", ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IG Markets connection validation failed for tenant {TenantId}", _tenantId);
            return false;
        }
    }

    public async Task<string> PlaceOrderAsync(TradeCommand cmd, CancellationToken ct)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                "Connector not initialized. Call InitializeAsync first.");
        }

        _logger.LogInformation(
            "Placing IG order for tenant {TenantId}: {Symbol} {Side} {Units} units @ {Price}, SL: {StopLoss}, TP: {TakeProfit}",
            _tenantId,
            cmd.Symbol,
            cmd.Side,
            cmd.Units,
            cmd.Price?.ToString() ?? "MARKET",
            cmd.StopLoss?.ToString() ?? "none",
            cmd.TakeProfit?.ToString() ?? "none");

        try
        {
            // 1. Convert symbol to IG epic
            string epic;
            try
            {
                epic = IGSymbolMapper.ConvertToEpic(cmd.Symbol);
                _logger.LogDebug("Converted symbol {Symbol} to epic {Epic}", cmd.Symbol, epic);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Symbol {Symbol} not supported by IG Markets", cmd.Symbol);
                throw new InvalidOperationException(
                    $"Symbol {cmd.Symbol} is not supported. " +
                    $"Supported symbols: {string.Join(", ", IGSymbolMapper.GetSupportedSymbols())}",
                    ex);
            }

            // 2. Build position request
            var request = new IGCreatePositionRequest
            {
                Epic = epic,
                Direction = cmd.Side == OrderSide.Buy ? "BUY" : "SELL",
                Size = cmd.Units,
                OrderType = cmd.Price.HasValue ? "LIMIT" : "MARKET",
                Level = cmd.Price,
                StopLevel = cmd.StopLoss,
                LimitLevel = cmd.TakeProfit,
                GuaranteedStop = false,
                ForceOpen = true,
                CurrencyCode = "USD"
            };

            // 3. Create position on IG
            var dealRef = await _apiClient.CreatePositionAsync(request, ct);

            _logger.LogInformation(
                "IG position created successfully for tenant {TenantId}. Deal reference: {DealReference}",
                _tenantId,
                dealRef.DealReference);

            // 4. Wait for deal confirmation (with timeout)
            var confirmation = await WaitForDealConfirmationAsync(
                dealRef.DealReference,
                timeoutSeconds: 10,
                ct);

            if (confirmation.DealStatus == "ACCEPTED")
            {
                var orderId = confirmation.DealId ?? dealRef.DealReference;

                _logger.LogInformation(
                    "IG order {OrderId} accepted for tenant {TenantId}, {Symbol}. Executed @ {Level}",
                    orderId,
                    _tenantId,
                    cmd.Symbol,
                    confirmation.Level);

                return orderId;
            }
            else
            {
                var reason = confirmation.Reason ?? "Unknown reason";

                _logger.LogError(
                    "IG order rejected for tenant {TenantId}, {Symbol}. Status: {Status}, Reason: {Reason}",
                    _tenantId,
                    cmd.Symbol,
                    confirmation.DealStatus,
                    reason);

                throw new InvalidOperationException(
                    $"IG rejected order for {cmd.Symbol}. Status: {confirmation.DealStatus}, Reason: {reason}");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Unexpected error placing IG order for tenant {TenantId}, {Symbol}", _tenantId, cmd.Symbol);
            throw new InvalidOperationException(
                $"Failed to place IG order for {cmd.Symbol}: {ex.Message}",
                ex);
        }
    }

    private async Task<IGDealConfirmation> WaitForDealConfirmationAsync(
        string dealReference,
        int timeoutSeconds,
        CancellationToken ct)
    {
        var startTime = DateTimeOffset.UtcNow;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var pollInterval = TimeSpan.FromMilliseconds(500);

        while (DateTimeOffset.UtcNow - startTime < timeout)
        {
            try
            {
                var confirmation = await _apiClient.GetDealConfirmationAsync(dealReference, ct);

                if (!string.IsNullOrEmpty(confirmation.DealStatus))
                {
                    return confirmation;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Error polling deal confirmation {DealReference}, will retry",
                    dealReference);
            }

            await Task.Delay(pollInterval, ct);
        }

        _logger.LogWarning(
            "Timeout waiting for deal confirmation {DealReference} after {Timeout}s",
            dealReference,
            timeoutSeconds);

        throw new TimeoutException(
            $"Timeout waiting for deal confirmation {dealReference} after {timeoutSeconds}s");
    }
}