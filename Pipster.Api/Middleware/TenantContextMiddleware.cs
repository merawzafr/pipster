namespace Pipster.Api.Middleware;

/// <summary>
/// Middleware to extract and set tenant context from JWT claims
/// </summary>
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(
        RequestDelegate next,
        ILogger<TenantContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantId = context.User.FindFirst("tenant_id")?.Value;
            var userId = context.User.FindFirst("sub")?.Value;
            var email = context.User.FindFirst("email")?.Value;

            if (!string.IsNullOrEmpty(tenantId))
            {
                // Store tenant context in HttpContext items for use in controllers/services
                context.Items["TenantId"] = tenantId;
                context.Items["UserId"] = userId;
                context.Items["UserEmail"] = email;

                // Add tenant ID to request headers for downstream services
                context.Request.Headers.Add("X-Tenant-Id", tenantId);

                _logger.LogDebug(
                    "Tenant context set for request: TenantId={TenantId}, UserId={UserId}",
                    tenantId, userId);
            }
            else
            {
                _logger.LogWarning(
                    "Authenticated user {UserId} has no tenant_id claim",
                    userId);
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension method to register the tenant context middleware
/// </summary>
public static class TenantContextMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantContextMiddleware>();
    }
}