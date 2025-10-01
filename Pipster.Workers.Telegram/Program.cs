using Pipster.Application.Handlers;
using Pipster.Application.Parsing;
using Pipster.Infrastructure.Messaging;
using Pipster.Infrastructure.Telegram;
using Pipster.Workers.Telegram;

var builder = Host.CreateApplicationBuilder(args);

// Add Telegram services
builder.Services.AddTelegramServices(builder.Configuration);

// Add message bus (using existing InMemoryBus)
builder.Services.AddSingleton<IMessageBus, InMemoryBus>();

// Add signal parser
builder.Services.AddSingleton<ISignalParser, RegexSignalParser>();

// Add tenant config provider (in-memory for now)
builder.Services.AddSingleton<ITenantConfigProvider, InMemoryTenantConfigProvider>();

// Add worker services
builder.Services.AddHostedService<TelegramWorkerService>();
builder.Services.AddHostedService<TelegramMessageHandlerWorker>();

var host = builder.Build();
host.Run();