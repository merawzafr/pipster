using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pipster.Application;
using Pipster.Connectors.IGMarkets;
using Pipster.Infrastructure;
using Pipster.Infrastructure.Messaging;
using Pipster.Infrastructure.Telegram;
using Pipster.Workers.Executor;
using Pipster.Workers.Telegram;

// ============================================================================
// PIPSTER HOST - COMPOSITION ROOT
// This is the ONLY place that knows about all implementations.
// Workers and infrastructure remain ignorant of specific connectors.
// ============================================================================

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Infrastructure services (repositories, factory, message bus)
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddSingleton<IMessageBus, InMemoryBus>();

        // Application services (orchestration, business logic)
        builder.Services.AddApplicationServices();

        // Telegram services (session management, client)
        builder.Services.AddTelegramServices(builder.Configuration);

        // ============================================================================
        // CONNECTOR REGISTRATION (Pluggable Architecture)
        // Add/remove connectors here without touching workers or infrastructure
        // ============================================================================
        builder.Services.AddIGMarketsConnector(builder.Configuration);
        // Future: builder.Services.AddOandaConnector(builder.Configuration);
        // Future: builder.Services.AddIBKRConnector(builder.Configuration);
        // Future: builder.Services.AddAlpacaConnector(builder.Configuration);

        // ============================================================================
        // WORKER REGISTRATION
        // Background services that process messages
        // ============================================================================
        builder.Services.AddHostedService<TelegramWorkerService>();
        builder.Services.AddHostedService<TelegramMessageHandlerWorker>();
        builder.Services.AddHostedService<TradeExecutor>();

        // ============================================================================
        // BUILD AND RUN
        // ============================================================================
        var host = builder.Build();

        // Seed test data in development
        if (builder.Environment.IsDevelopment())
        {
            await host.Services.SeedTestDataAsync();
        }

        await host.RunAsync();
    }
}