namespace Pipster.Application.Handlers;

/// <summary>
/// Provides tenant-specific configuration for signal processing.
/// </summary>
public interface ITenantConfigProvider
{
    Task<TenantConfig?> GetConfigAsync(string tenantId, CancellationToken ct);
}

/// <summary>
/// Tenant configuration for signal processing and validation.
/// </summary>
public record TenantConfig
{
    public required string TenantId { get; init; }
    public required string RegexPattern { get; init; }
    public required IReadOnlyList<long> ObservedChannels { get; init; }
    public required IReadOnlyList<string> WhitelistedSymbols { get; init; }

    // Future: Add more configuration options
    // public TimeOnly? TradingSessionStart { get; init; }
    // public TimeOnly? TradingSessionEnd { get; init; }
    // public int MaxConcurrentPositions { get; init; }
    // public decimal MaxRiskPerTrade { get; init; }
}