namespace Pipster.Shared.Contracts.Telegram;

/// <summary>
/// Request to remove a channel from observation list
/// </summary>
public record RemoveChannelRequest
{
    public required string TenantId { get; init; }
    public required long ChannelId { get; init; }
}