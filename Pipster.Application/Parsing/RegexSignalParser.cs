using System.Globalization;
using System.Text.RegularExpressions;
using Pipster.Shared.Contracts;
using Pipster.Shared.Enums;

namespace Pipster.Application.Parsing;

public sealed class RegexSignalParser : ISignalParser
{
    public NormalizedSignal? TryParse(string regex, string signal)
    {
        if (string.IsNullOrWhiteSpace(regex) || string.IsNullOrWhiteSpace(signal))
            return null;

        try
        {
            // Clean the signal: trim and remove invisible characters
            var cleanedSignal = CleanSignal(signal);

            // Remove all whitespace and non-printable characters for regex matching
            var normalizedSignal = Regex.Replace(cleanedSignal, @"\s+", "", RegexOptions.IgnoreCase);

            var match = Regex.Match(normalizedSignal, regex, RegexOptions.IgnoreCase);
            if (!match.Success)
                return null;

            // Extract components using named groups
            var symbol = ExtractSymbol(match);
            var side = ExtractSide(match);
            var entry = ExtractEntry(match);
            var stopLoss = ExtractStopLoss(match);
            var takeProfits = ExtractTakeProfits(match);

            if (string.IsNullOrEmpty(symbol))
                return null;

            return new NormalizedSignal(
                TenantId: "default", // Placeholder, replace with actual tenant ID if available
                Source: "unknown", // Placeholder, replace with actual source if available
                Symbol: symbol,
                Side: side,
                Entry: entry,
                StopLoss: stopLoss,
                TakeProfits: takeProfits,
                SeenAt: DateTimeOffset.UtcNow,
                RawText: cleanedSignal,
                Hash: Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(cleanedSignal)))
            );
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string CleanSignal(string signal)
    {
        // Remove invisible characters (control characters, zero-width spaces, etc.)
        var cleaned = Regex.Replace(signal, @"[\u0000-\u001F\u007F-\u009F\u200B-\u200D\uFEFF]", "");
        return cleaned.Trim();
    }

    private static string? ExtractSymbol(Match match)
    {
        // Try different possible symbol group names
        var symbolGroupNames = new[] { "symbol", "pair", "instrument" };

        foreach (var groupName in symbolGroupNames)
        {
            if (match.Groups[groupName].Success)
            {
                var symbol = match.Groups[groupName].Value;
                // Clean up symbol (remove # and other prefixes)
                symbol = Regex.Replace(symbol, @"[#@]", "");
                return symbol.ToUpperInvariant();
            }
        }

        // Fallback: look for common forex/commodity patterns
        var symbolMatch = Regex.Match(match.Value, @"(XAU|XAG|EUR|GBP|USD|JPY|AUD|CAD|CHF|NZD|GOLD|SILVER)[A-Z]{3,6}", RegexOptions.IgnoreCase);
        if (symbolMatch.Success)
        {
            return symbolMatch.Value.ToUpperInvariant();
        }

        return null;
    }

    private static OrderSide ExtractSide(Match match)
    {
        var sideGroupNames = new[] { "side", "direction", "action" };

        foreach (var groupName in sideGroupNames)
        {
            if (match.Groups[groupName].Success)
            {
                var sideValue = match.Groups[groupName].Value.ToLowerInvariant();
                return sideValue.Contains("buy") || sideValue.Contains("long") ? OrderSide.Buy : OrderSide.Sell;
            }
        }

        // Fallback: search in the entire match
        var fullMatch = match.Value.ToLowerInvariant();
        if (fullMatch.Contains("buy") || fullMatch.Contains("long"))
            return OrderSide.Buy;

        return OrderSide.Sell; // Default to sell for short/sell signals
    }

    private static decimal? ExtractEntry(Match match)
    {
        var entryGroupNames = new[] { "entry", "price" };

        foreach (var groupName in entryGroupNames)
        {
            if (match.Groups[groupName].Success &&
                decimal.TryParse(match.Groups[groupName].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var entry))
            {
                return entry;
            }
        }

        return null; // Market entry
    }

    private static decimal? ExtractStopLoss(Match match)
    {
        var slGroupNames = new[] { "sl", "stoploss", "stop" };

        foreach (var groupName in slGroupNames)
        {
            if (match.Groups[groupName].Success &&
                decimal.TryParse(match.Groups[groupName].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var sl))
            {
                return sl;
            }
        }

        return null;
    }

    private static IReadOnlyList<decimal> ExtractTakeProfits(Match match)
    {
        var takeProfits = new List<decimal>();

        // Look for multiple TP groups (tp1, tp2, tp3, etc.)
        for (int i = 1; i <= 10; i++)
        {
            var tpGroupName = $"tp{i}";
            if (match.Groups[tpGroupName].Success &&
                decimal.TryParse(match.Groups[tpGroupName].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var tp))
            {
                takeProfits.Add(tp);
            }
        }

        // Also look for generic tp group and target groups
        var genericTpGroups = new[] { "tp", "target", "target1", "target2", "target3" };
        foreach (var groupName in genericTpGroups)
        {
            if (match.Groups[groupName].Success &&
                decimal.TryParse(match.Groups[groupName].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var tp))
            {
                if (!takeProfits.Contains(tp))
                    takeProfits.Add(tp);
            }
        }

        return takeProfits.AsReadOnly();
    }
}
