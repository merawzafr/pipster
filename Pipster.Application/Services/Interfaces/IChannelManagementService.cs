using Pipster.Domain.Entities;

namespace Pipster.Application.Services;

/// <summary>
/// Application service for managing Telegram channel subscriptions.
/// Coordinates channel configuration, tenant updates, and message bus notifications.
/// </summary>
public interface IChannelManagementService
{
    /// <summary>
    /// Adds a channel to a tenant's monitoring list
    /// </summary>
    Task<ChannelConfiguration> AddChannelAsync(
        string tenantId,
        long channelId,
        string regexPattern,
        string? channelName = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a channel from a tenant's monitoring list
    /// </summary>
    Task RemoveChannelAsync(
        string tenantId,
        long channelId,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the regex pattern for a channel
    /// </summary>
    Task UpdateChannelRegexAsync(
        string tenantId,
        long channelId,
        string newRegexPattern,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the channel name
    /// </summary>
    Task UpdateChannelNameAsync(
        string tenantId,
        long channelId,
        string? newName,
        CancellationToken ct = default);

    /// <summary>
    /// Enables signal parsing for a channel
    /// </summary>
    Task EnableChannelAsync(
        string tenantId,
        long channelId,
        CancellationToken ct = default);

    /// <summary>
    /// Disables signal parsing for a channel
    /// </summary>
    Task DisableChannelAsync(
        string tenantId,
        long channelId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves all channels for a tenant
    /// </summary>
    Task<IReadOnlyList<ChannelConfiguration>> GetChannelsAsync(
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves enabled channels for a tenant
    /// </summary>
    Task<IReadOnlyList<ChannelConfiguration>> GetEnabledChannelsAsync(
        string tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a specific channel configuration
    /// </summary>
    Task<ChannelConfiguration?> GetChannelConfigAsync(
        string tenantId,
        long channelId,
        CancellationToken ct = default);
}