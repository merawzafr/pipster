using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pipster.Application.Parsing;
using Pipster.Application.Services;
using Pipster.Domain.Entities;
using Pipster.Domain.Enums;

namespace Pipster.Application;

/// <summary>
/// Dependency injection extensions for application layer services.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers application services (orchestration and business logic).
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register application services
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IChannelManagementService, ChannelManagementService>();
        services.AddScoped<ITradingConfigService, TradingConfigService>();

        // Register cached tenant config provider
        services.AddSingleton<ICachedTenantConfigProvider, CachedTenantConfigProvider>();

        // Register signal parser
        services.AddSingleton<ISignalParser, RegexSignalParser>();

        return services;
    }

    /// <summary>
    /// Seeds initial test data for development.
    /// DO NOT use in production!
    /// </summary>
    public static async Task SeedTestDataAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
        var channelService = scope.ServiceProvider.GetRequiredService<IChannelManagementService>();
        var tradingService = scope.ServiceProvider.GetRequiredService<ITradingConfigService>();

        // Create test tenant
        var tenant = await tenantService.CreateTenantAsync(
            id: "test-tenant-1",
            email: "test@pipster.dev",
            displayName: "Test Tenant",
            plan: SubscriptionPlan.Pro);

        // Set Telegram credentials (using placeholder values)
        await tenantService.SetTelegramCredentialsAsync(
            tenantId: tenant.Id,
            apiId: 12345, // TODO: Replace with real values
            apiHash: "test_api_hash");

        // Create default trading configuration
        var tradingConfig = await tradingService.CreateDefaultConfigAsync(tenant.Id);

        // Configure trading settings
        await tradingService.SetFixedSizingAsync(tenant.Id, units: 1000);
        await tradingService.WhitelistSymbolAsync(tenant.Id, "XAUUSD");
        await tradingService.WhitelistSymbolAsync(tenant.Id, "EURUSD");
        await tradingService.EnableAutoExecutionAsync(tenant.Id);

        // Add a test channel
        await channelService.AddChannelAsync(
            tenantId: tenant.Id,
            channelId: 123456789, // TODO: Replace with real channel ID
            regexPattern: @"(?<side>buy|sell)#?(?<symbol>[A-Z]{6,}).*?(?<entry>\d+\.?\d*)-?\d*\.?\d*.*?sl(?<sl>\d+\.?\d*).*?tp(?<tp1>\d+\.?\d*).*?tp(?<tp2>\d+\.?\d*).*?tp(?<tp3>\d+\.?\d*)",
            channelName: "Test Signals Channel");
    }
}