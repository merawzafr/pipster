namespace Pipster.Infrastructure.Telegram;

/// <summary>
/// Manages Telegram session persistence.
/// Sessions allow reconnection without re-authentication.
/// </summary>
public interface ITelegramSessionStore
{
    Task<string> GetSessionPathAsync(string tenantId, CancellationToken ct);
    Task<bool> SessionExistsAsync(string tenantId, CancellationToken ct);
    Task DeleteSessionAsync(string tenantId, CancellationToken ct);
    Task<byte[]?> DownloadSessionFileAsync(string tenantId, string fileName, CancellationToken ct);
    Task UploadSessionFileAsync(string tenantId, string fileName, byte[] content, CancellationToken ct);
}