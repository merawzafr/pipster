using Pipster.Domain.Enums;

namespace Pipster.Domain.Entities;

/// <summary>
/// Represents a broker connection configuration for a tenant.
/// Each tenant can have multiple broker connections for comparison/redundancy.
/// </summary>
public sealed class BrokerConnection
{
    private readonly Dictionary<string, string> _metadata = new();

    public string Id { get; private set; }
    public string TenantId { get; private set; }

    /// <summary>
    /// Broker type identifier: "IGMarkets", "OANDA", "IBKR", etc.
    /// </summary>
    public BrokerType BrokerType { get; private set; }

    /// <summary>
    /// User-friendly name for this connection (e.g., "IG Demo Account", "OANDA Live")
    /// </summary>
    public string DisplayName { get; private set; }

    /// <summary>
    /// Whether this connection is active and should be used
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Encrypted credentials stored as JSON
    /// Will be decrypted in-memory when creating connector
    /// </summary>
    public string EncryptedCredentials { get; private set; }

    /// <summary>
    /// Additional metadata (account ID, account type, base URL, etc.)
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata => _metadata;

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }

    // Private constructor for EF/serialization
    private BrokerConnection()
    {
        Id = string.Empty;
        TenantId = string.Empty;
        DisplayName = string.Empty;
        EncryptedCredentials = string.Empty;
    }

    /// <summary>
    /// Creates a new broker connection
    /// </summary>
    public static BrokerConnection Create(
        string tenantId,
        BrokerType brokerType,
        string displayName,
        string encryptedCredentials)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant ID cannot be empty", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty", nameof(displayName));

        if (string.IsNullOrWhiteSpace(encryptedCredentials))
            throw new ArgumentException("Credentials cannot be empty", nameof(encryptedCredentials));

        return new BrokerConnection
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            BrokerType = brokerType,
            DisplayName = displayName,
            EncryptedCredentials = encryptedCredentials,
            IsActive = true, // Active by default
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Updates the encrypted credentials
    /// </summary>
    public void UpdateCredentials(string encryptedCredentials)
    {
        if (string.IsNullOrWhiteSpace(encryptedCredentials))
            throw new ArgumentException("Credentials cannot be empty", nameof(encryptedCredentials));

        EncryptedCredentials = encryptedCredentials;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Updates the display name
    /// </summary>
    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty", nameof(displayName));

        DisplayName = displayName;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Activates this broker connection
    /// </summary>
    public void Activate()
    {
        if (!IsActive)
        {
            IsActive = true;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Deactivates this broker connection
    /// </summary>
    public void Deactivate()
    {
        if (IsActive)
        {
            IsActive = false;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Updates the last used timestamp
    /// </summary>
    public void MarkAsUsed()
    {
        LastUsedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Adds or updates metadata
    /// </summary>
    public void SetMetadata(string key, string value)
    {
        _metadata[key] = value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Removes metadata
    /// </summary>
    public void RemoveMetadata(string key)
    {
        _metadata.Remove(key);
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}