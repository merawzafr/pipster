using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pipster.Shared.Contracts.Telegram;

namespace Pipster.Infrastructure.Telegram;

/// <summary>
/// Dependency injection extensions for Telegram integration.
/// </summary>
public static class TelegramServiceCollectionExtensions
{
    public static IServiceCollection AddTelegramServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<TelegramClientOptions>(configuration.GetSection("Telegram"));

        // Register as singleton for direct access
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<TelegramClientOptions>>().Value);

        // Azure Blob Storage for session persistence
        var blobConnectionString = configuration.GetConnectionString("AzureBlobStorage");
        if (!string.IsNullOrEmpty(blobConnectionString))
        {
            services.AddSingleton(sp => new BlobServiceClient(blobConnectionString));
            services.AddSingleton<ITelegramSessionStore, AzureBlobSessionStore>();
        }
        else
        {
            // Fallback to local file system for development
            services.AddSingleton<ITelegramSessionStore, LocalFileSessionStore>();
        }

        // Core services
        services.AddSingleton<ITelegramClientManager, TelegramClientManager>();

        return services;
    }
}