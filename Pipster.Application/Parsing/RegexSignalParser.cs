using Pipster.Shared.Contracts;
using System.Text.RegularExpressions;
using Pipster.Shared.Enums;

public sealed class RegexSignalParser : ISignalParser
{
    // Example pattern: "Sell #XAUUSD 3689-3693 SL 3700 TP 3687 3685 3680"
    static readonly Regex Rx = new(
        @"(?<side>buy|sell)\s+#?(?<sym>[A-Z0-9/._-]+)\s+(?<entry>\d+(\.\d+)?)(?:-\d+(\.\d+)?)?\s*sl\s*(?<sl>\d+(\.\d+)?)\s*tp\s*(?<tps>(\d+(\.\d+)?\s*)+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public NormalizedSignal? TryParse(string tenantId, string source, string text)
    {
        var m = Rx.Match(text.Replace(",", "."));
        if (!m.Success) return null;

        var side = m.Groups["side"].Value.Equals("buy", StringComparison.OrdinalIgnoreCase) ? OrderSide.Buy : OrderSide.Sell;
        var sym = m.Groups["sym"].Value.ToUpperInvariant();
        var entry = decimal.TryParse(m.Groups["entry"].Value, out var e) ? e : (decimal?)null;
        var sl = decimal.TryParse(m.Groups["sl"].Value, out var s) ? s : (decimal?)null;

        var tps = m.Groups["tps"].Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => decimal.Parse(x))
            .ToList();

        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

        return new NormalizedSignal(
            TenantId: tenantId,
            Source: source,
            Symbol: sym,
            Side: side,
            Entry: entry,
            StopLoss: sl,
            TakeProfits: tps,
            SeenAt: DateTimeOffset.UtcNow,
            RawText: text,
            Hash: hash
        );
    }
}
