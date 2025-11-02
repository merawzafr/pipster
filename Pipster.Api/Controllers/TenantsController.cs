using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pipster.Api.Services;
using Pipster.Application.Services;
using Pipster.Domain.Entities;
using Pipster.Domain.Enums;

namespace Pipster.Api.Controllers;

/// <summary>
/// Tenant management endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireTenant")]  // Requires authenticated user with tenant claim
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly IChannelManagementService _channelService;
    private readonly ITradingConfigService _tradingService;
    private readonly ITenantContextService _tenantContext;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        ITenantService tenantService,
        IChannelManagementService channelService,
        ITradingConfigService tradingService,
        ITenantContextService tenantContext,
        ILogger<TenantsController> logger)
    {
        _tenantService = tenantService;
        _channelService = channelService;
        _tradingService = tradingService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new tenant (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireAdmin")]  // Only admins can create tenants
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

            _logger.LogInformation(
                "Tenant created: {TenantId} by user {UserId}",
                tenant.Id, _tenantContext.UserId);

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
    /// Gets a tenant by ID (must be own tenant or admin)
    /// </summary>
    [HttpGet("{tenantId}")]
    public async Task<IActionResult> GetTenant(string tenantId)
    {
        // Validate access: user can only access their own tenant unless admin
        if (_tenantContext.TenantId != tenantId && !User.IsInRole("Admin"))
        {
            _logger.LogWarning(
                "Unauthorized tenant access attempt: User {UserId} tried to access tenant {TenantId}",
                _tenantContext.UserId, tenantId);
            return Forbid();
        }

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
    /// Gets current user's tenant
    /// </summary>
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentTenant()
    {
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrEmpty(tenantId))
        {
            return BadRequest(new { error = "No tenant context found" });
        }

        return await GetTenant(tenantId);
    }

    /// <summary>
    /// Adds a channel to a tenant's monitoring list
    /// </summary>
    [HttpPost("{tenantId}/channels")]
    public async Task<IActionResult> AddChannel(
        string tenantId,
        [FromBody] AddChannelRequest request)
    {
        // Validate access
        if (_tenantContext.TenantId != tenantId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        try
        {
            var config = await _channelService.AddChannelAsync(
                tenantId: tenantId,
                channelId: request.ChannelId,
                regexPattern: request.RegexPattern,
                channelName: request.ChannelName);

            _logger.LogInformation(
                "Channel {ChannelId} added to tenant {TenantId} by user {UserId}",
                request.ChannelId, tenantId, _tenantContext.UserId);

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
        // Validate access
        if (_tenantContext.TenantId != tenantId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

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
        // Validate access
        if (_tenantContext.TenantId != tenantId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

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
        // Validate access
        if (_tenantContext.TenantId != tenantId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        try
        {
            await _channelService.RemoveChannelAsync(tenantId, channelId);

            _logger.LogInformation(
                "Channel {ChannelId} removed from tenant {TenantId} by user {UserId}",
                channelId, tenantId, _tenantContext.UserId);

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
        // Validate access
        if (_tenantContext.TenantId != tenantId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

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
        // Validate access
        if (_tenantContext.TenantId != tenantId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

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

            _logger.LogInformation(
                "Trading config updated for tenant {TenantId} by user {UserId}",
                tenantId, _tenantContext.UserId);

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
        // Validate access
        if (_tenantContext.TenantId != tenantId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        try
        {
            await _tradingService.WhitelistSymbolAsync(tenantId, symbol);

            _logger.LogInformation(
                "Symbol {Symbol} whitelisted for tenant {TenantId} by user {UserId}",
                symbol, tenantId, _tenantContext.UserId);

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
        // Validate access
        if (_tenantContext.TenantId != tenantId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        try
        {
            await _tradingService.BlacklistSymbolAsync(tenantId, symbol);

            _logger.LogInformation(
                "Symbol {Symbol} blacklisted for tenant {TenantId} by user {UserId}",
                symbol, tenantId, _tenantContext.UserId);

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