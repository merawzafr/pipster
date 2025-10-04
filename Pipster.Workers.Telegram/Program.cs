using Pipster.Application;
using Pipster.Infrastructure;
using Pipster.Infrastructure.Idempotency;
using Pipster.Infrastructure.Telegram;
using Pipster.Workers.Telegram;

var builder = Host.CreateApplicationBuilder(args);

// Add Telegram services (TDLib client management)
builder.Services.AddTelegramServices(builder.Configuration);

// Add infrastructure services (repositories)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add application services (orchestration, signal parsing)
builder.Services.AddApplicationServices();

// Add message bus
builder.Services.AddSingleton<Pipster.Infrastructure.Messaging.IMessageBus,
    Pipster.Infrastructure.Messaging.InMemoryBus>();

// Add worker services
builder.Services.AddHostedService<TelegramWorkerService>();
builder.Services.AddHostedService<TelegramMessageHandlerWorker>();

var host = builder.Build();

// Seed test data in development
if (builder.Environment.IsDevelopment())
{
    await host.Services.SeedTestDataAsync();
}

host.Run();