using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pipster.Connectors.IGMarkets.Configuration;
using Pipster.Connectors.IGMarkets.Services;
using Pipster.Shared.Contracts;
using Polly;
using Polly.Extensions.Http;

namespace Pipster.Connectors.IGMarkets;

/// <summary>
/// Dependency injection extensions for IG Markets connector
/// </summary>
public static class IGMarketsServiceCollectionExtensions
{
    /// <summary>
    /// Adds IG Markets connector services to the service collection
    /// </summary>
    public static IServiceCollection AddIGMarketsConnector(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<IGMarketsOptions>(
            configuration.GetSection("IGMarkets"));

        // Validate configuration on startup
        services.AddSingleton<IValidateOptions<IGMarketsOptions>, IGMarketsOptionsValidator>();

        // Register as transient so factory can create new instances
        services.AddSingleton<ITradeConnectorProvider, IGMarketsConnectorProvider>();

        // Register session manager with its own HttpClient
        services.AddHttpClient<IIGSessionManager, IGSessionManager>()
            .ConfigureHttpClient((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<IGMarketsOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        // Register session manager as singleton (maintains session state)
        services.AddSingleton<IIGSessionManager, IGSessionManager>();

        // Register API client with its own HttpClient
        services.AddHttpClient<IIGMarketsApiClient, IGMarketsApiClient>()
            .ConfigureHttpClient((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<IGMarketsOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        // Register the main connector
        services.AddSingleton<ITradeConnector, IGMarketsConnector>();

        return services;
    }

    /// <summary>
    /// Gets retry policy for transient HTTP failures
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    // Logging will be handled by the HttpClient logging
                });
    }

    /// <summary>
    /// Gets circuit breaker policy to prevent overwhelming the API
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }
}

/// <summary>
/// Validates IGMarketsOptions configuration
/// </summary>
internal class IGMarketsOptionsValidator : IValidateOptions<IGMarketsOptions>
{
    public ValidateOptionsResult Validate(string? name, IGMarketsOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return ValidateOptionsResult.Fail("IGMarkets:ApiKey is required");
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            return ValidateOptionsResult.Fail("IGMarkets:Username is required");
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            return ValidateOptionsResult.Fail("IGMarkets:Password is required");
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            return ValidateOptionsResult.Fail("IGMarkets:BaseUrl is required");
        }

        if (options.TimeoutSeconds <= 0)
        {
            return ValidateOptionsResult.Fail("IGMarkets:TimeoutSeconds must be greater than 0");
        }

        return ValidateOptionsResult.Success;
    }
}