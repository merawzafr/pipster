using System.Reflection;
using Microsoft.OpenApi.Models;
using Pipster.Api;
using Pipster.Api.Extensions;
using Pipster.Api.Middleware;
using Pipster.Api.Services;
using Pipster.Application;
using Pipster.Infrastructure;
using Pipster.Infrastructure.Telegram;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add Authentication and Authorization
builder.Services.AddPipsterAuthentication(builder.Configuration);
builder.Services.AddPipsterAuthorization();

// Add Tenant Context
builder.Services.AddTenantContext();

// Add CORS for Identity Server and frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("PipsterCors", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5000",  // Identity Server
                "http://localhost:3000",  // Next.js frontend
                "https://identity.pipster.app",
                "https://pipster.app",
                "https://www.pipster.app")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

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
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new[] { "pipster.api" }
        }
    });

    var xml = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xml);
    c.IncludeXmlComments(xmlPath);
});

builder.Services.AddOpenApi();
builder.Services.AddSingleton<PipsterState>();

// Add health checks
builder.Services.AddHealthChecks();

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
        opts.OAuthClientId("pipster-web");
        opts.OAuthScopes("openid", "profile", "email", "tenant", "pipster.api");
        opts.OAuthUsePkce();
    });
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Configure middleware pipeline
app.UseHttpsRedirection();

// CORS must be before authentication
app.UseCors("PipsterCors");

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Custom middleware for tenant context
app.UseTenantContext();

// Health check endpoints
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();

app.MapControllers();

app.Run();