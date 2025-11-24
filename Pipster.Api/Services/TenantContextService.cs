namespace Pipster.Api.Services;

/// <summary>
/// Service to provide access to the current tenant context
/// </summary>
public interface ITenantContextService
{
    string? TenantId { get; }
    string? UserId { get; }
    string? UserEmail { get; }
    bool IsAuthenticated { get; }
}

/// <summary>
/// Implementation that retrieves tenant context from HttpContext
/// </summary>
public class TenantContextService : ITenantContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? TenantId =>
        _httpContextAccessor.HttpContext?.Items["TenantId"] as string
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("tenant_id")?.Value;

    public string? UserId =>
        _httpContextAccessor.HttpContext?.Items["UserId"] as string
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;

    public string? UserEmail =>
        _httpContextAccessor.HttpContext?.Items["UserEmail"] as string
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value;

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}

/// <summary>
/// Extension method to register tenant context services
/// </summary>
public static class TenantContextServiceExtensions
{
    public static IServiceCollection AddTenantContext(
        this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContextService, TenantContextService>();
        return services;
    }
}