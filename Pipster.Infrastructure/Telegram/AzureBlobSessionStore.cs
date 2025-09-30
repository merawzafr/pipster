using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace Pipster.Infrastructure.Telegram;

/// <summary>
/// Manages Telegram session persistence in Azure Blob Storage.
/// Each tenant gets their own isolated session directory.
/// </summary>
public class AzureBlobSessionStore : ITelegramSessionStore
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobSessionStore> _logger;
    private readonly string _localCachePath;

    public AzureBlobSessionStore(
        BlobServiceClient blobServiceClient,
        ILogger<AzureBlobSessionStore> logger,
        string? localCachePath = null)
    {
        _containerClient = blobServiceClient.GetBlobContainerClient("telegram-sessions");
        _logger = logger;
        _localCachePath = localCachePath ?? Path.Combine(Path.GetTempPath(), "pipster-sessions");

        Directory.CreateDirectory(_localCachePath);
    }

    public async Task<string> GetSessionPathAsync(string tenantId, CancellationToken ct)
    {
        var localPath = Path.Combine(_localCachePath, tenantId);
        Directory.CreateDirectory(localPath);

        // Download existing session files from blob storage if they exist
        await SyncFromBlobAsync(tenantId, localPath, ct);

        return localPath;
    }

    public async Task<bool> SessionExistsAsync(string tenantId, CancellationToken ct)
    {
        var prefix = $"{tenantId}/";

        await foreach (var blob in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: ct))
        {
            return true; // At least one file exists
        }

        return false;
    }

    public async Task DeleteSessionAsync(string tenantId, CancellationToken ct)
    {
        var prefix = $"{tenantId}/";

        await foreach (var blob in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: ct))
        {
            await _containerClient.DeleteBlobIfExistsAsync(blob.Name, cancellationToken: ct);
        }

        var localPath = Path.Combine(_localCachePath, tenantId);
        if (Directory.Exists(localPath))
        {
            Directory.Delete(localPath, recursive: true);
        }

        _logger.LogInformation("Deleted session for tenant {TenantId}", tenantId);
    }

    public async Task<byte[]?> DownloadSessionFileAsync(string tenantId, string fileName, CancellationToken ct)
    {
        var blobName = $"{tenantId}/{fileName}";
        var blobClient = _containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync(ct))
            return null;

        using var stream = new MemoryStream();
        await blobClient.DownloadToAsync(stream, ct);
        return stream.ToArray();
    }

    public async Task UploadSessionFileAsync(string tenantId, string fileName, byte[] content, CancellationToken ct)
    {
        var blobName = $"{tenantId}/{fileName}";
        var blobClient = _containerClient.GetBlobClient(blobName);

        using var stream = new MemoryStream(content);
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: ct);

        _logger.LogDebug("Uploaded session file {FileName} for tenant {TenantId}", fileName, tenantId);
    }

    private async Task SyncFromBlobAsync(string tenantId, string localPath, CancellationToken ct)
    {
        var prefix = $"{tenantId}/";

        await foreach (var blob in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: ct))
        {
            var fileName = blob.Name.Substring(prefix.Length);
            var localFilePath = Path.Combine(localPath, fileName);

            // Only download if file doesn't exist locally or is older than blob
            if (!File.Exists(localFilePath) ||
                File.GetLastWriteTimeUtc(localFilePath) < blob.Properties.LastModified?.UtcDateTime)
            {
                var blobClient = _containerClient.GetBlobClient(blob.Name);
                await blobClient.DownloadToAsync(localFilePath, ct);

                _logger.LogDebug("Synced {FileName} from blob for tenant {TenantId}", fileName, tenantId);
            }
        }
    }
}