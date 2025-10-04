using Microsoft.Extensions.Logging;
using Pipster.Domain.Entities;
using Pipster.Domain.Enums;
using Pipster.Domain.Repositories;
using Pipster.Infrastructure.Messaging;
using Pipster.Shared.Contracts.Telegram;

namespace Pipster.Application.Services;

/// <summary>
/// Implementation of channel management application service.
/// Coordinates channel configuration, tenant updates, and Telegram worker notifications.
/// </summary>
public sealed class ChannelManagementService : IChannelManagementService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IChannelConfigurationRepository _channelRepository;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<ChannelManagementService> _logger;

    public ChannelManagementService(
        ITenantRepository tenantRepository,
        IChannelConfigurationRepository channelRepository,
        IMessageBus messageBus,
        ILogger<ChannelManagementService> logger)
    {
        _tenantRepository = tenantRepository;
        _channelRepository = channelRepository;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<ChannelConfiguration> AddChannelAsync(
        string tenantId,
        long channelId,
        string regexPattern,
        string? channelName = null,
        CancellationToken ct = default)
    {
        // Validate tenant exists and is active
        var tenant = await GetActiveTenantOrThrowAsync(tenantId, ct);

        // Check if channel already exists
        var existing = await _channelRepository.GetByTenantAndChannelAsync(tenantId, channelId, ct);
        if (existing != null)
        {
            throw new InvalidOperationException(
                $"Channel {channelId} is already configured for tenant {tenantId}");
        }

        // Create channel configuration using domain factory
        var config = ChannelConfiguration.Create(tenantId, channelId, regexPattern, channelName);

        // Add to tenant's channel list (domain logic)
        tenant.AddChannel(channelId.ToString());

        // Persist both entities
        await _channelRepository.AddAsync(config, ct);
        await _tenantRepository.UpdateAsync(tenant, ct);

        // Notify Telegram worker to start monitoring
        await _messageBus.PublishChannelRequestAsync(new AddChannelRequest
        {
            TenantId = tenantId,
            ChannelId = channelId,
            ChannelName = channelName
        }, ct);

        _logger.LogInformation(
            "Added channel {ChannelId} for tenant {TenantId} with regex pattern",
            channelId, tenantId);

        return config;
    }

    public async Task RemoveChannelAsync(
        string tenantId,
        long channelId,
        CancellationToken ct = default)
    {
        // Validate tenant exists
        var tenant = await GetTenantOrThrowAsync(tenantId, ct);

        // Get channel configuration
        var config = await _channelRepository.GetByTenantAndChannelAsync(tenantId, channelId, ct);
        if (config == null)
        {
            throw new InvalidOperationException(
                $"Channel {channelId} not found for tenant {tenantId}");
        }

        // Remove from tenant's channel list (domain logic)
        tenant.RemoveChannel(channelId.ToString());

        // Delete channel configuration
        await _channelRepository.DeleteAsync(config.Id, ct);
        await _tenantRepository.UpdateAsync(tenant, ct);

        // Notify Telegram worker to stop monitoring
        await _messageBus.PublishRemoveChannelRequestAsync(new RemoveChannelRequest
        {
            TenantId = tenantId,
            ChannelId = channelId
        }, ct);

        _logger.LogInformation(
            "Removed channel {ChannelId} for tenant {TenantId}",
            channelId, tenantId);
    }

    public async Task UpdateChannelRegexAsync(
        string tenantId,
        long channelId,
        string newRegexPattern,
        CancellationToken ct = default)
    {
        var config = await GetChannelConfigOrThrowAsync(tenantId, channelId, ct);

        // Domain method handles regex validation
        config.UpdateRegexPattern(newRegexPattern);

        await _channelRepository.UpdateAsync(config, ct);

        _logger.LogInformation(
            "Updated regex pattern for channel {ChannelId} of tenant {TenantId}",
            channelId, tenantId);
    }

    public async Task UpdateChannelNameAsync(
        string tenantId,
        long channelId,
        string? newName,
        CancellationToken ct = default)
    {
        var config = await GetChannelConfigOrThrowAsync(tenantId, channelId, ct);

        config.UpdateChannelName(newName);

        await _channelRepository.UpdateAsync(config, ct);

        _logger.LogInformation(
            "Updated name for channel {ChannelId} of tenant {TenantId} to {NewName}",
            channelId, tenantId, newName);
    }

    public async Task EnableChannelAsync(
        string tenantId,
        long channelId,
        CancellationToken ct = default)
    {
        var config = await GetChannelConfigOrThrowAsync(tenantId, channelId, ct);

        config.Enable();

        await _channelRepository.UpdateAsync(config, ct);

        _logger.LogInformation(
            "Enabled channel {ChannelId} for tenant {TenantId}",
            channelId, tenantId);
    }

    public async Task DisableChannelAsync(
        string tenantId,
        long channelId,
        CancellationToken ct = default)
    {
        var config = await GetChannelConfigOrThrowAsync(tenantId, channelId, ct);

        config.Disable();

        await _channelRepository.UpdateAsync(config, ct);

        _logger.LogInformation(
            "Disabled channel {ChannelId} for tenant {TenantId}",
            channelId, tenantId);
    }

    public async Task<IReadOnlyList<ChannelConfiguration>> GetChannelsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        return await _channelRepository.GetByTenantIdAsync(tenantId, ct);
    }

    public async Task<IReadOnlyList<ChannelConfiguration>> GetEnabledChannelsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        return await _channelRepository.GetEnabledByTenantIdAsync(tenantId, ct);
    }

    public async Task<ChannelConfiguration?> GetChannelConfigAsync(
        string tenantId,
        long channelId,
        CancellationToken ct = default)
    {
        return await _channelRepository.GetByTenantAndChannelAsync(tenantId, channelId, ct);
    }

    private async Task<Tenant> GetTenantOrThrowAsync(string tenantId, CancellationToken ct)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, ct);
        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant '{tenantId}' not found");
        }
        return tenant;
    }

    private async Task<Tenant> GetActiveTenantOrThrowAsync(string tenantId, CancellationToken ct)
    {
        var tenant = await GetTenantOrThrowAsync(tenantId, ct);
        if (tenant.Status != TenantStatus.Active)
        {
            throw new InvalidOperationException(
                $"Tenant '{tenantId}' is not active (status: {tenant.Status})");
        }
        return tenant;
    }

    private async Task<ChannelConfiguration> GetChannelConfigOrThrowAsync(
        string tenantId,
        long channelId,
        CancellationToken ct)
    {
        var config = await _channelRepository.GetByTenantAndChannelAsync(tenantId, channelId, ct);
        if (config == null)
        {
            throw new InvalidOperationException(
                $"Channel {channelId} not found for tenant {tenantId}");
        }
        return config;
    }
}