using System.Reflection;
using Microsoft.OpenApi.Models;
using Pipster.Api;
using Pipster.Application;
using Pipster.Infrastructure;
using Pipster.Infrastructure.Telegram;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add services
builder.Services.AddTelegramServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Pipster API",
        Version = "v1",
        Description = "Endpoints for status, signals, and execution."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference{ Type = ReferenceType.SecurityScheme, Id = "Bearer" }},
            Array.Empty<string>()
        }
    });

    var xml = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xml);
    c.IncludeXmlComments(xmlPath);
});

builder.Services.AddOpenApi();
builder.Services.AddSingleton<PipsterState>();

var app = builder.Build();

// Seed test data in development
if (app.Environment.IsDevelopment())
{
    await app.Services.SeedTestDataAsync();
}

// Enable Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(opts =>
    {
        opts.SwaggerEndpoint("/swagger/v1/swagger.json", "Pipster API v1");
    });
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();