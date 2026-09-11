using System.Net.Http;
using System.Text.Json;

namespace TickerBoard;

public sealed class YahooMarketDataProvider : IMarketDataProvider
{
    private static readonly HttpClient _http = new();

    private static string ToYahooSymbol(SymbolSetting s)
    {
        if (string.Equals(s.Market, "JP", StringComparison.OrdinalIgnoreCase))
        {
            // Yahoo Finance uses .T suffix for Tokyo listings
            return s.Symbol.Contains('.') ? s.Symbol : s.Symbol + ".T";
        }
        // For US and ETF assume symbol as-is
        return s.Symbol;
    }

    public async Task<IReadOnlyList<StockQuote>> GetQuotesAsync(IReadOnlyList<SymbolSetting> symbols, CancellationToken cancellationToken = default)
    {
        if (symbols == null || symbols.Count == 0) return Array.Empty<StockQuote>();

        // Build symbol list for Yahoo Finance
        var yahooSymbols = symbols.Select(ToYahooSymbol).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var chunk = string.Join(',', yahooSymbols);
        var url = $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={Uri.EscapeDataString(chunk)}";

        try
        {
            await using var stream = await _http.GetStreamAsync(url, cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!doc.RootElement.TryGetProperty("quoteResponse", out var qr) || !qr.TryGetProperty("result", out var results)) return Array.Empty<StockQuote>();

            var list = new List<StockQuote>();
            foreach (var item in results.EnumerateArray())
            {
                try
                {
                    var symbol = item.GetProperty("symbol").GetString() ?? string.Empty;
                    var name = item.TryGetProperty("shortName", out var sn) ? sn.GetString() ?? symbol : symbol;
                    if (string.IsNullOrEmpty(name) && item.TryGetProperty("longName", out var ln)) name = ln.GetString() ?? symbol;
                    var price = item.TryGetProperty("regularMarketPrice", out var rp) && rp.ValueKind != JsonValueKind.Null ? rp.GetDecimal() : 0m;
                    var change = item.TryGetProperty("regularMarketChange", out var rc) && rc.ValueKind != JsonValueKind.Null ? rc.GetDecimal() : 0m;
                    var changePercent = item.TryGetProperty("regularMarketChangePercent", out var rcp) && rcp.ValueKind != JsonValueKind.Null ? rcp.GetDecimal() : 0m;
                    var currency = item.TryGetProperty("currency", out var cur) ? (cur.GetString() ?? "USD") : "USD";

                    // Convert Yahoo symbol back to requested form if needed (e.g., 7203.T -> 7203)
                    var originalSymbol = symbol;
                    if (symbol.EndsWith(".T", StringComparison.OrdinalIgnoreCase)) originalSymbol = symbol.Substring(0, symbol.Length - 2);

                    list.Add(new StockQuote(originalSymbol, name, price, change, changePercent, currency == "JPY" ? "JPY" : "USD"));
                }
                catch { /* skip malformed item */ }
            }

            // Preserve requested order: map back to provided symbols order
            var ordered = symbols.Select(s => list.FirstOrDefault(q => string.Equals(q.Symbol, s.Symbol, StringComparison.OrdinalIgnoreCase)) ?? new StockQuote(s.Symbol, s.Name, 0m, 0m, 0m, s.Market == "JP" ? "JPY" : "USD")).ToList();
            return ordered;
        }
        catch
        {
            // On error, return zeroed quotes so UI still has entries
            return symbols.Select(s => new StockQuote(s.Symbol, s.Name, 0m, 0m, 0m, s.Market == "JP" ? "JPY" : "USD")).ToList();
        }
    }
}
