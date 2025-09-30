using Pipster.Infrastructure.Telegram;
using Pipster.Workers.Telegram;

var builder = Host.CreateApplicationBuilder(args);

// Add Telegram services
builder.Services.AddTelegramServices(builder.Configuration);

// Add message bus (using existing InMemoryBus)
builder.Services.AddSingleton<Pipster.Infrastructure.Messaging.IMessageBus, Pipster.Infrastructure.Messaging.InMemoryBus>();

// Add worker service
builder.Services.AddHostedService<TelegramWorkerService>();

var host = builder.Build();
host.Run();