using Microsoft.Extensions.Logging;

namespace Pipster.Infrastructure.Telegram;

/// <summary>
/// Local file system implementation of session store for development.
/// DO NOT use in production - no redundancy or backup.
/// </summary>
public class LocalFileSessionStore : ITelegramSessionStore
{
    private readonly ILogger<LocalFileSessionStore> _logger;
    private readonly string _basePath;

    public LocalFileSessionStore(
        ILogger<LocalFileSessionStore> logger,
        string? basePath = null)
    {
        _logger = logger;
        _basePath = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Pipster",
            "TelegramSessions");

        Directory.CreateDirectory(_basePath);
    }

    public Task<string> GetSessionPathAsync(string tenantId, CancellationToken ct)
    {
        var path = Path.Combine(_basePath, tenantId);
        Directory.CreateDirectory(path);
        return Task.FromResult(path);
    }

    public Task<bool> SessionExistsAsync(string tenantId, CancellationToken ct)
    {
        var path = Path.Combine(_basePath, tenantId);
        var exists = Directory.Exists(path) && Directory.GetFiles(path).Length > 0;
        return Task.FromResult(exists);
    }

    public Task DeleteSessionAsync(string tenantId, CancellationToken ct)
    {
        var path = Path.Combine(_basePath, tenantId);
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
            _logger.LogInformation("Deleted local session for tenant {TenantId}", tenantId);
        }
        return Task.CompletedTask;
    }

    public Task<byte[]?> DownloadSessionFileAsync(string tenantId, string fileName, CancellationToken ct)
    {
        var filePath = Path.Combine(_basePath, tenantId, fileName);
        if (!File.Exists(filePath))
            return Task.FromResult<byte[]?>(null);

        var bytes = File.ReadAllBytes(filePath);
        return Task.FromResult<byte[]?>(bytes);
    }

    public Task UploadSessionFileAsync(string tenantId, string fileName, byte[] content, CancellationToken ct)
    {
        var directoryPath = Path.Combine(_basePath, tenantId);
        Directory.CreateDirectory(directoryPath);

        var filePath = Path.Combine(directoryPath, fileName);
        File.WriteAllBytes(filePath, content);

        return Task.CompletedTask;
    }
}