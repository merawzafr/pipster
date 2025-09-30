namespace Pipster.Shared.Contracts.Telegram;

/// <summary>
/// Configuration options for Telegram client behavior
/// </summary>
public record TelegramClientOptions
{
    public int MaxReconnectAttempts { get; init; } = 5;
    public TimeSpan MessageTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan HealthCheckInterval { get; init; } = TimeSpan.FromMinutes(1);
    public int MaxConcurrentClients { get; init; } = 1000;
    public TimeSpan IdleClientTimeout { get; init; } = TimeSpan.FromHours(1);
}
