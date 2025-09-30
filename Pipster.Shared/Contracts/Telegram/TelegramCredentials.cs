namespace Pipster.Shared.Contracts.Telegram;

/// <summary>
/// Credentials required to connect to Telegram
/// </summary>
public record TelegramCredentials
{
    public required int ApiId { get; init; }
    public required string ApiHash { get; init; }
    public string? PhoneNumber { get; init; }
}
