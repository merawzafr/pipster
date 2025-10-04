using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pipster.Domain.Repositories;
using Pipster.Infrastructure.Idempotency;
using Pipster.Infrastructure.Messaging;
using Pipster.Infrastructure.Repositories;
using StackExchange.Redis;

namespace Pipster.Infrastructure;

/// <summary>
/// Dependency injection extensions for infrastructure services (repositories only).
/// Application services are registered in Pipster.Application layer.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers infrastructure services.
    /// Uses in-memory implementations by default - swap to SQL/Cosmos for production.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register repositories (in-memory for MVP)
        services.AddSingleton<ITenantRepository, InMemoryTenantRepository>();
        services.AddSingleton<IChannelConfigurationRepository, InMemoryChannelConfigurationRepository>();
        services.AddSingleton<ITradingConfigurationRepository, InMemoryTradingConfigurationRepository>();
        services.AddSingleton<IMessageBus, InMemoryBus>();

        /// Dependency injection extensions for idempotency services.
        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            // Use Redis for production
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnectionString));

            services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        }
        else
        {
            // Use in-memory for development
            services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        }


        return services;
    }
}