namespace Pipster.Infrastructure.Telegram;

/// <summary>
/// Telegram client interface for a single tenant.
/// Handles connection lifecycle, message observation, and channel management.
/// </summary>
public interface ITelegramClient : IAsyncDisposable
{
    string TenantId { get; }
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken ct);
    Task<bool> AuthenticateAsync(string phoneNumber, CancellationToken ct);
    Task<bool> VerifyCodeAsync(string code, CancellationToken ct);
    Task AddChannelAsync(long channelId, CancellationToken ct);
    Task RemoveChannelAsync(long channelId, CancellationToken ct);
    Task<IReadOnlyList<long>> GetObservedChannelsAsync(CancellationToken ct);
}