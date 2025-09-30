namespace Pipster.Shared.Contracts.Telegram;

/// <summary>
/// Message published when a new Telegram message is received from an observed channel.
/// </summary>
public record TelegramMessageReceived
{
    public required string TenantId { get; init; }
    public required long ChannelId { get; init; }
    public required long MessageId { get; init; }
    public required string Content { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required long SenderId { get; init; }

    /// <summary>
    /// Unique identifier for idempotency checking
    /// </summary>
    public string MessageKey => $"{TenantId}:{ChannelId}:{MessageId}";
}