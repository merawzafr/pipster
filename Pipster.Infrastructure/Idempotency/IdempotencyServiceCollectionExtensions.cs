using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Pipster.Infrastructure.Idempotency;

/// <summary>
/// Dependency injection extensions for idempotency services.
/// </summary>
public static class IdempotencyServiceCollectionExtensions
{
    public static IServiceCollection AddIdempotencyStore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
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