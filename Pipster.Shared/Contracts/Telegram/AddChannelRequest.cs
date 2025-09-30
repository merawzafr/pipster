namespace Pipster.Shared.Contracts.Telegram;

/// <summary>
/// Request to add a channel to observation list
/// </summary>
public record AddChannelRequest
{
    public required string TenantId { get; init; }
    public required long ChannelId { get; init; }
    public string? ChannelName { get; init; }
}
