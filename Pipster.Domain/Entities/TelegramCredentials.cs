namespace Pipster.Domain.Entities;

/// <summary>
/// Telegram credentials (stored encrypted)
/// </summary>
public sealed record TelegramCredentials
{
    public required int ApiId { get; init; }
    public required string ApiHash { get; init; }
    public string? PhoneNumber { get; init; }
}