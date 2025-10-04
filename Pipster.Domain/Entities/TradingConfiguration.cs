using Pipster.Domain.Enums;

namespace Pipster.Domain.Entities;

/// <summary>
/// Trading configuration and risk parameters for a tenant.
/// Defines what/when to trade and position sizing.
/// </summary>
public sealed class TradingConfiguration
{
    private readonly HashSet<string> _whitelistedSymbols = new();
    private readonly HashSet<string> _blacklistedSymbols = new();

    public string Id { get; private set; }
    public string TenantId { get; private set; }

    /// <summary>
    /// Symbols allowed for trading (empty = all allowed)
    /// </summary>
    public IReadOnlySet<string> WhitelistedSymbols => _whitelistedSymbols;

    /// <summary>
    /// Symbols explicitly forbidden
    /// </summary>
    public IReadOnlySet<string> BlacklistedSymbols => _blacklistedSymbols;

    /// <summary>
    /// Position sizing strategy
    /// </summary>
    public PositionSizingMode SizingMode { get; private set; }

    /// <summary>
    /// Fixed units per trade (when SizingMode = Fixed)
    /// </summary>
    public int? FixedUnits { get; private set; }

    /// <summary>
    /// Percentage of equity per trade (when SizingMode = PercentEquity)
    /// Range: 0.01 - 100.0
    /// </summary>
    public decimal? EquityPercentage { get; private set; }

    /// <summary>
    /// Maximum total exposure across all positions (% of equity)
    /// </summary>
    public decimal MaxTotalExposurePercent { get; private set; }

    /// <summary>
    /// Maximum slippage tolerance (pips/points)
    /// </summary>
    public decimal? MaxSlippagePips { get; private set; }

    /// <summary>
    /// Trading session constraints (UTC times)
    /// </summary>
    public TradingSession? TradingSession { get; private set; }

    /// <summary>
    /// Whether to auto-execute signals (if false, signals are only logged)
    /// </summary>
    public bool AutoExecuteEnabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    // Private constructor for EF/serialization
    private TradingConfiguration()
    {
        Id = string.Empty;
        TenantId = string.Empty;
    }

    /// <summary>
    /// Creates default trading configuration for a tenant
    /// </summary>
    public static TradingConfiguration CreateDefault(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant ID cannot be empty", nameof(tenantId));

        return new TradingConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            SizingMode = PositionSizingMode.Fixed,
            FixedUnits = 1000, // Micro lot
            MaxTotalExposurePercent = 10.0m, // 10% max exposure
            AutoExecuteEnabled = false, // Safe default
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Configures fixed position sizing
    /// </summary>
    public void SetFixedSizing(int units)
    {
        if (units <= 0)
            throw new ArgumentException("Units must be positive", nameof(units));

        SizingMode = PositionSizingMode.Fixed;
        FixedUnits = units;
        EquityPercentage = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Configures percentage-based position sizing
    /// </summary>
    public void SetPercentageSizing(decimal equityPercent)
    {
        if (equityPercent <= 0 || equityPercent > 100)
            throw new ArgumentException("Equity percentage must be between 0.01 and 100", nameof(equityPercent));

        SizingMode = PositionSizingMode.PercentEquity;
        EquityPercentage = equityPercent;
        FixedUnits = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets maximum total exposure limit
    /// </summary>
    public void SetMaxExposure(decimal maxExposurePercent)
    {
        if (maxExposurePercent <= 0 || maxExposurePercent > 100)
            throw new ArgumentException("Max exposure must be between 0.01 and 100", nameof(maxExposurePercent));

        MaxTotalExposurePercent = maxExposurePercent;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Adds a symbol to the whitelist
    /// </summary>
    public void WhitelistSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));

        _whitelistedSymbols.Add(symbol.ToUpperInvariant());
        _blacklistedSymbols.Remove(symbol.ToUpperInvariant());
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Removes a symbol from the whitelist
    /// </summary>
    public void RemoveFromWhitelist(string symbol)
    {
        _whitelistedSymbols.Remove(symbol.ToUpperInvariant());
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Adds a symbol to the blacklist
    /// </summary>
    public void BlacklistSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));

        _blacklistedSymbols.Add(symbol.ToUpperInvariant());
        _whitelistedSymbols.Remove(symbol.ToUpperInvariant());
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Removes a symbol from the blacklist
    /// </summary>
    public void RemoveFromBlacklist(string symbol)
    {
        _blacklistedSymbols.Remove(symbol.ToUpperInvariant());
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Checks if a symbol is allowed for trading
    /// </summary>
    public bool IsSymbolAllowed(string symbol)
    {
        var upperSymbol = symbol.ToUpperInvariant();

        // Blacklist takes precedence
        if (_blacklistedSymbols.Contains(upperSymbol))
            return false;

        // If whitelist is empty, all symbols allowed (except blacklisted)
        if (_whitelistedSymbols.Count == 0)
            return true;

        // Otherwise, must be in whitelist
        return _whitelistedSymbols.Contains(upperSymbol);
    }

    /// <summary>
    /// Sets trading session hours (UTC)
    /// </summary>
    public void SetTradingSession(TimeOnly startUtc, TimeOnly endUtc, DayOfWeek[]? allowedDays = null)
    {
        TradingSession = new TradingSession(startUtc, endUtc, allowedDays);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Removes trading session restrictions
    /// </summary>
    public void ClearTradingSession()
    {
        TradingSession = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Checks if trading is allowed at the current time
    /// </summary>
    public bool IsTradingAllowedNow(DateTimeOffset now)
    {
        if (TradingSession == null)
            return true;

        return TradingSession.IsWithinSession(now);
    }

    /// <summary>
    /// Enables auto-execution of signals
    /// </summary>
    public void EnableAutoExecution()
    {
        AutoExecuteEnabled = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Disables auto-execution (signals will only be logged)
    /// </summary>
    public void DisableAutoExecution()
    {
        AutoExecuteEnabled = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets maximum slippage tolerance
    /// </summary>
    public void SetMaxSlippage(decimal pips)
    {
        if (pips < 0)
            throw new ArgumentException("Slippage cannot be negative", nameof(pips));

        MaxSlippagePips = pips;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}