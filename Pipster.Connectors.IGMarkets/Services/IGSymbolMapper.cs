using Microsoft.Extensions.Logging;

namespace Pipster.Connectors.IGMarkets.Services;

/// <summary>
/// Maps standard trading symbols to IG Market epics
/// </summary>
public static class IGSymbolMapper
{
    private static readonly IReadOnlyDictionary<string, string> SymbolToEpicMap = new Dictionary<string, string>
    {
        // Forex majors
        ["EURUSD"] = "CS.D.EURUSD.CFD.IP",
        ["GBPUSD"] = "CS.D.GBPUSD.CFD.IP",
        ["USDJPY"] = "CS.D.USDJPY.CFD.IP",
        ["USDCHF"] = "CS.D.USDCHF.CFD.IP",
        ["AUDUSD"] = "CS.D.AUDUSD.CFD.IP",
        ["USDCAD"] = "CS.D.USDCAD.CFD.IP",
        ["NZDUSD"] = "CS.D.NZDUSD.CFD.IP",

        // Forex crosses
        ["EURGBP"] = "CS.D.EURGBP.CFD.IP",
        ["EURJPY"] = "CS.D.EURJPY.CFD.IP",
        ["GBPJPY"] = "CS.D.GBPJPY.CFD.IP",

        // Precious metals
        ["XAUUSD"] = "CS.D.CFDGOLD.CFD.IP",   // Gold vs USD
        ["XAGUSD"] = "CS.D.CFDSILVER.CFD.IP", // Silver vs USD

        // Indices (examples)
        ["US30"] = "IX.D.DOW.IFD.IP",         // Dow Jones
        ["SPX500"] = "IX.D.SPTRD.IFD.IP",     // S&P 500
        ["NAS100"] = "IX.D.NASDAQ.IFD.IP",    // NASDAQ 100
    };

    private static readonly IReadOnlyDictionary<string, string> EpicToSymbolMap =
        SymbolToEpicMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    /// <summary>
    /// Converts a standard symbol to IG epic
    /// </summary>
    /// <param name="symbol">Standard symbol (e.g., XAUUSD)</param>
    /// <returns>IG epic (e.g., CS.D.CFDGOLD.CFD.IP)</returns>
    /// <exception cref="ArgumentException">If symbol is not mapped</exception>
    public static string ConvertToEpic(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));

        var upperSymbol = symbol.ToUpperInvariant();

        if (SymbolToEpicMap.TryGetValue(upperSymbol, out var epic))
        {
            return epic;
        }

        throw new ArgumentException(
            $"Symbol '{symbol}' is not mapped to an IG epic. " +
            $"Available symbols: {string.Join(", ", SymbolToEpicMap.Keys)}",
            nameof(symbol));
    }

    /// <summary>
    /// Converts an IG epic back to standard symbol
    /// </summary>
    /// <param name="epic">IG epic (e.g., CS.D.CFDGOLD.CFD.IP)</param>
    /// <returns>Standard symbol (e.g., XAUUSD)</returns>
    public static string? ConvertToSymbol(string epic)
    {
        if (string.IsNullOrWhiteSpace(epic))
            return null;

        return EpicToSymbolMap.TryGetValue(epic, out var symbol) ? symbol : null;
    }

    /// <summary>
    /// Checks if a symbol is supported
    /// </summary>
    public static bool IsSymbolSupported(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return false;

        return SymbolToEpicMap.ContainsKey(symbol.ToUpperInvariant());
    }

    /// <summary>
    /// Gets all supported symbols
    /// </summary>
    public static IReadOnlyList<string> GetSupportedSymbols()
    {
        return SymbolToEpicMap.Keys.ToList();
    }
}