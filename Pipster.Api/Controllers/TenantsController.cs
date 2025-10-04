using Microsoft.AspNetCore.Mvc;
using Pipster.Application.Services;
using Pipster.Domain.Entities;
using Pipster.Domain.Enums;

namespace Pipster.Api.Controllers;

/// <summary>
/// Tenant management endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly IChannelManagementService _channelService;
    private readonly ITradingConfigService _tradingService;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        ITenantService tenantService,
        IChannelManagementService channelService,
        ITradingConfigService tradingService,
        ILogger<TenantsController> logger)
    {
        _tenantService = tenantService;
        _channelService = channelService;
        _tradingService = tradingService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new tenant
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
    {
        try
        {
            var tenant = await _tenantService.CreateTenantAsync(
                id: request.Id,
                email: request.Email,
                displayName: request.DisplayName,
                plan: request.Plan);

            // Create default trading configuration
            await _tradingService.CreateDefaultConfigAsync(tenant.Id);

            return CreatedAtAction(
                nameof(GetTenant),
                new { tenantId = tenant.Id },
                new { tenant.Id, tenant.Email, tenant.DisplayName, tenant.Plan, tenant.Status });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets a tenant by ID
    /// </summary>
    [HttpGet("{tenantId}")]
    public async Task<IActionResult> GetTenant(string tenantId)
    {
        var tenant = await _tenantService.GetTenantAsync(tenantId);
        if (tenant == null)
        {
            return NotFound(new { error = $"Tenant '{tenantId}' not found" });
        }

        return Ok(new
        {
            tenant.Id,
            tenant.Email,
            tenant.DisplayName,
            tenant.Plan,
            tenant.Status,
            tenant.CreatedAt,
            tenant.SubscribedChannelIds
        });
    }

    /// <summary>
    /// Adds a channel to a tenant's monitoring list
    /// </summary>
    [HttpPost("{tenantId}/channels")]
    public async Task<IActionResult> AddChannel(
        string tenantId,
        [FromBody] AddChannelRequest request)
    {
        try
        {
            var config = await _channelService.AddChannelAsync(
                tenantId: tenantId,
                channelId: request.ChannelId,
                regexPattern: request.RegexPattern,
                channelName: request.ChannelName);

            return CreatedAtAction(
                nameof(GetChannel),
                new { tenantId, channelId = config.ChannelId },
                new
                {
                    config.Id,
                    config.ChannelId,
                    config.ChannelName,
                    config.RegexPattern,
                    config.IsEnabled
                });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets a specific channel configuration
    /// </summary>
    [HttpGet("{tenantId}/channels/{channelId}")]
    public async Task<IActionResult> GetChannel(string tenantId, long channelId)
    {
        var config = await _channelService.GetChannelConfigAsync(tenantId, channelId);
        if (config == null)
        {
            return NotFound(new { error = $"Channel {channelId} not found for tenant {tenantId}" });
        }

        return Ok(new
        {
            config.Id,
            config.ChannelId,
            config.ChannelName,
            config.RegexPattern,
            config.IsEnabled,
            config.CreatedAt,
            config.UpdatedAt
        });
    }

    /// <summary>
    /// Gets all channels for a tenant
    /// </summary>
    [HttpGet("{tenantId}/channels")]
    public async Task<IActionResult> GetChannels(string tenantId)
    {
        var configs = await _channelService.GetChannelsAsync(tenantId);
        return Ok(configs.Select(c => new
        {
            c.Id,
            c.ChannelId,
            c.ChannelName,
            c.RegexPattern,
            c.IsEnabled
        }));
    }

    /// <summary>
    /// Removes a channel from a tenant's monitoring list
    /// </summary>
    [HttpDelete("{tenantId}/channels/{channelId}")]
    public async Task<IActionResult> RemoveChannel(string tenantId, long channelId)
    {
        try
        {
            await _channelService.RemoveChannelAsync(tenantId, channelId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets trading configuration for a tenant
    /// </summary>
    [HttpGet("{tenantId}/trading-config")]
    public async Task<IActionResult> GetTradingConfig(string tenantId)
    {
        var config = await _tradingService.GetConfigAsync(tenantId);
        if (config == null)
        {
            return NotFound(new { error = $"Trading configuration not found for tenant {tenantId}" });
        }

        return Ok(new
        {
            config.Id,
            config.TenantId,
            config.SizingMode,
            config.FixedUnits,
            config.EquityPercentage,
            config.MaxTotalExposurePercent,
            config.MaxSlippagePips,
            config.AutoExecuteEnabled,
            WhitelistedSymbols = config.WhitelistedSymbols.ToList(),
            BlacklistedSymbols = config.BlacklistedSymbols.ToList(),
            TradingSession = config.TradingSession != null ? new
            {
                config.TradingSession.StartUtc,
                config.TradingSession.EndUtc,
                config.TradingSession.AllowedDays
            } : null
        });
    }

    /// <summary>
    /// Updates trading configuration
    /// </summary>
    [HttpPatch("{tenantId}/trading-config")]
    public async Task<IActionResult> UpdateTradingConfig(
        string tenantId,
        [FromBody] UpdateTradingConfigRequest request)
    {
        try
        {
            if (request.SizingMode.HasValue)
            {
                if (request.SizingMode == PositionSizingMode.Fixed && request.FixedUnits.HasValue)
                {
                    await _tradingService.SetFixedSizingAsync(tenantId, request.FixedUnits.Value);
                }
                else if (request.SizingMode == PositionSizingMode.PercentEquity && request.EquityPercentage.HasValue)
                {
                    await _tradingService.SetPercentageSizingAsync(tenantId, request.EquityPercentage.Value);
                }
            }

            if (request.MaxTotalExposurePercent.HasValue)
            {
                await _tradingService.SetMaxExposureAsync(tenantId, request.MaxTotalExposurePercent.Value);
            }

            if (request.MaxSlippagePips.HasValue)
            {
                await _tradingService.SetMaxSlippageAsync(tenantId, request.MaxSlippagePips.Value);
            }

            if (request.AutoExecuteEnabled.HasValue)
            {
                if (request.AutoExecuteEnabled.Value)
                    await _tradingService.EnableAutoExecutionAsync(tenantId);
                else
                    await _tradingService.DisableAutoExecutionAsync(tenantId);
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Adds a symbol to the whitelist
    /// </summary>
    [HttpPost("{tenantId}/trading-config/whitelist/{symbol}")]
    public async Task<IActionResult> WhitelistSymbol(string tenantId, string symbol)
    {
        try
        {
            await _tradingService.WhitelistSymbolAsync(tenantId, symbol);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Adds a symbol to the blacklist
    /// </summary>
    [HttpPost("{tenantId}/trading-config/blacklist/{symbol}")]
    public async Task<IActionResult> BlacklistSymbol(string tenantId, string symbol)
    {
        try
        {
            await _tradingService.BlacklistSymbolAsync(tenantId, symbol);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

// Request DTOs
public record CreateTenantRequest(
    string Id,
    string Email,
    string DisplayName,
    SubscriptionPlan Plan);

public record AddChannelRequest(
    long ChannelId,
    string RegexPattern,
    string? ChannelName);

public record UpdateTradingConfigRequest(
    PositionSizingMode? SizingMode,
    int? FixedUnits,
    decimal? EquityPercentage,
    decimal? MaxTotalExposurePercent,
    decimal? MaxSlippagePips,
    bool? AutoExecuteEnabled);