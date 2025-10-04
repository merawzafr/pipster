using Pipster.Domain.Enums;

namespace Pipster.Domain.Entities;

/// <summary>
/// Represents a customer/subscriber in the Pipster platform.
/// Aggregate root for tenant-related operations.
/// </summary>
public sealed class Tenant
{
    private readonly List<string> _subscribedChannelIds = new();

    public string Id { get; private set; }
    public string Email { get; private set; }
    public string DisplayName { get; private set; }
    public TenantStatus Status { get; private set; }
    public SubscriptionPlan Plan { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeactivatedAt { get; private set; }

    /// <summary>
    /// Telegram credentials for this tenant (encrypted in storage)
    /// </summary>
    public TelegramCredentials? TelegramCredentials { get; private set; }

    /// <summary>
    /// Channels this tenant is monitoring
    /// </summary>
    public IReadOnlyList<string> SubscribedChannelIds => _subscribedChannelIds.AsReadOnly();

    // Private constructor for EF/serialization
    private Tenant()
    {
        Id = string.Empty;
        Email = string.Empty;
        DisplayName = string.Empty;
    }

    /// <summary>
    /// Creates a new tenant (factory method)
    /// </summary>
    public static Tenant Create(string id, string email, string displayName, SubscriptionPlan plan)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Tenant ID cannot be empty", nameof(id));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty", nameof(email));

        return new Tenant
        {
            Id = id,
            Email = email,
            DisplayName = displayName,
            Status = TenantStatus.Active,
            Plan = plan,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Sets Telegram credentials for this tenant
    /// </summary>
    public void SetTelegramCredentials(int apiId, string apiHash, string? phoneNumber = null)
    {
        if (apiId <= 0)
            throw new ArgumentException("API ID must be positive", nameof(apiId));

        if (string.IsNullOrWhiteSpace(apiHash))
            throw new ArgumentException("API Hash cannot be empty", nameof(apiHash));

        TelegramCredentials = new TelegramCredentials
        {
            ApiId = apiId,
            ApiHash = apiHash,
            PhoneNumber = phoneNumber
        };
    }

    /// <summary>
    /// Adds a channel to the monitoring list
    /// </summary>
    public void AddChannel(string channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
            throw new ArgumentException("Channel ID cannot be empty", nameof(channelId));

        if (Status != TenantStatus.Active)
            throw new InvalidOperationException($"Cannot add channels to {Status} tenant");

        if (!_subscribedChannelIds.Contains(channelId))
        {
            _subscribedChannelIds.Add(channelId);
        }
    }

    /// <summary>
    /// Removes a channel from the monitoring list
    /// </summary>
    public void RemoveChannel(string channelId)
    {
        _subscribedChannelIds.Remove(channelId);
    }

    /// <summary>
    /// Deactivates the tenant (soft delete)
    /// </summary>
    public void Deactivate()
    {
        if (Status == TenantStatus.Active)
        {
            Status = TenantStatus.Inactive;
            DeactivatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Reactivates a previously deactivated tenant
    /// </summary>
    public void Reactivate()
    {
        if (Status == TenantStatus.Inactive)
        {
            Status = TenantStatus.Active;
            DeactivatedAt = null;
        }
    }

    /// <summary>
    /// Upgrades or downgrades the subscription plan
    /// </summary>
    public void ChangePlan(SubscriptionPlan newPlan)
    {
        Plan = newPlan;
    }
}