namespace Pipster.Domain.Entities;

/// <summary>
/// Configuration for a single Telegram channel being monitored.
/// Value object owned by Tenant aggregate.
/// </summary>
public sealed class ChannelConfiguration
{
    public string Id { get; private set; }
    public string TenantId { get; private set; }

    /// <summary>
    /// Telegram channel ID (numeric)
    /// </summary>
    public long ChannelId { get; private set; }

    /// <summary>
    /// Human-readable channel name (optional)
    /// </summary>
    public string? ChannelName { get; private set; }

    /// <summary>
    /// Regex pattern for parsing signals from this channel
    /// </summary>
    public string RegexPattern { get; private set; }

    /// <summary>
    /// Whether signal parsing is enabled for this channel
    /// </summary>
    public bool IsEnabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    // Private constructor for EF/serialization
    private ChannelConfiguration()
    {
        Id = string.Empty;
        TenantId = string.Empty;
        RegexPattern = string.Empty;
    }

    /// <summary>
    /// Creates a new channel configuration (factory method)
    /// </summary>
    public static ChannelConfiguration Create(
        string tenantId,
        long channelId,
        string regexPattern,
        string? channelName = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant ID cannot be empty", nameof(tenantId));

        if (channelId <= 0)
            throw new ArgumentException("Channel ID must be positive", nameof(channelId));

        if (string.IsNullOrWhiteSpace(regexPattern))
            throw new ArgumentException("Regex pattern cannot be empty", nameof(regexPattern));

        // Validate regex pattern
        try
        {
            _ = System.Text.RegularExpressions.Regex.Match("test", regexPattern);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException("Invalid regex pattern", nameof(regexPattern), ex);
        }

        return new ChannelConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            ChannelId = channelId,
            ChannelName = channelName,
            RegexPattern = regexPattern,
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Updates the regex pattern for signal parsing
    /// </summary>
    public void UpdateRegexPattern(string newPattern)
    {
        if (string.IsNullOrWhiteSpace(newPattern))
            throw new ArgumentException("Regex pattern cannot be empty", nameof(newPattern));

        // Validate regex pattern
        try
        {
            _ = System.Text.RegularExpressions.Regex.Match("test", newPattern);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException("Invalid regex pattern", nameof(newPattern), ex);
        }

        RegexPattern = newPattern;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Updates the channel name
    /// </summary>
    public void UpdateChannelName(string? newName)
    {
        ChannelName = newName;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Enables signal parsing for this channel
    /// </summary>
    public void Enable()
    {
        if (!IsEnabled)
        {
            IsEnabled = true;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Disables signal parsing for this channel
    /// </summary>
    public void Disable()
    {
        if (IsEnabled)
        {
            IsEnabled = false;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}