using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace Pipster.Api.Extensions
{
    public static class AuthenticationExtensions
    {
        public static IServiceCollection AddPipsterAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var identityServerUrl = configuration["Authentication:Authority"]
                ?? "http://localhost:5000";

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    // URL of your Identity Server
                    options.Authority = identityServerUrl;

                    // Audience validation - must match the API scope
                    options.Audience = "pipster.api";

                    // In development, allow HTTP (remove in production)
                    options.RequireHttpsMetadata = false;

                    // Token validation parameters
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = true,
                        ValidateIssuer = true,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(5),

                        // Map custom claims
                        NameClaimType = "name",
                        RoleClaimType = "role"
                    };

                    // Events for debugging
                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILogger<Program>>();
                            logger.LogError(context.Exception,
                                "Authentication failed: {Error}",
                                context.Exception.Message);
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILogger<Program>>();

                            var tenantId = context.Principal?.FindFirst("tenant_id")?.Value;
                            var userId = context.Principal?.FindFirst("sub")?.Value;

                            logger.LogInformation(
                                "Token validated for user {UserId} with tenant {TenantId}",
                                userId, tenantId);

                            return Task.CompletedTask;
                        },
                        OnChallenge = context =>
                        {
                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILogger<Program>>();
                            logger.LogWarning(
                                "Authentication challenge: {Error} - {ErrorDescription}",
                                context.Error, context.ErrorDescription);
                            return Task.CompletedTask;
                        }
                    };
                });

            return services;
        }

        public static IServiceCollection AddPipsterAuthorization(
            this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                // Default policy - requires authenticated user
                options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

                // Tenant-specific policy
                options.AddPolicy("RequireTenant", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("tenant_id");
                });

                // Admin policy (for future use)
                options.AddPolicy("RequireAdmin", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole("Admin");
                });

                // Specific tenant policy factory (can be used dynamically)
                options.AddPolicy("RequireSpecificTenant", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireAssertion(context =>
                    {
                        var tenantId = context.User.FindFirst("tenant_id")?.Value;
                        // You can add logic here to validate specific tenant access
                        return !string.IsNullOrEmpty(tenantId);
                    });
                });
            });

            return services;
        }
    }
}