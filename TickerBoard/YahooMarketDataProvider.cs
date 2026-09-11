using System.Net.Http;
using YahooFinanceApi;


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
        try
        {
            var securities = await Yahoo.Symbols(yahooSymbols.ToArray())
                .Fields(Field.Symbol, Field.ShortName, Field.RegularMarketPrice, Field.RegularMarketChange, Field.RegularMarketChangePercent, Field.Currency)
                .QueryAsync();

            var list = new List<StockQuote>();
            foreach (var s in yahooSymbols)
            {
                if (securities.TryGetValue(s, out var data))
                {
                    string symbolStr = s;
                    string name = s;
                    try { name = data.ShortName ?? data.LongName ?? s; } catch { }
                    decimal price = 0m;
                    try { price = data.RegularMarketPrice != null ? Convert.ToDecimal(data.RegularMarketPrice) : 0m; } catch { }
                    decimal change = 0m;
                    try { change = data.RegularMarketChange != null ? Convert.ToDecimal(data.RegularMarketChange) : 0m; } catch { }
                    decimal changePercent = 0m;
                    try { changePercent = data.RegularMarketChangePercent != null ? Convert.ToDecimal(data.RegularMarketChangePercent) : 0m; } catch { }
                    string currency = "USD";
                    try { currency = data.Currency ?? "USD"; } catch { }

                    // map back 7203.T -> 7203
                    if (symbolStr.EndsWith(".T", StringComparison.OrdinalIgnoreCase)) symbolStr = symbolStr.Substring(0, symbolStr.Length - 2);

                    list.Add(new StockQuote(symbolStr, name ?? symbolStr, price, change, changePercent, currency == "JPY" ? "JPY" : "USD"));
                }
                else
                {
                    // missing data
                    var orig = symbols.FirstOrDefault(x => string.Equals(ToYahooSymbol(x), s, StringComparison.OrdinalIgnoreCase));
                    if (orig != null) list.Add(new StockQuote(orig.Symbol, orig.Name, 0m, 0m, 0m, orig.Market == "JP" ? "JPY" : "USD"));
                }
            }

            // Preserve requested order based on original symbols
            var ordered = symbols.Select(s => list.FirstOrDefault(q => string.Equals(q.Symbol, s.Symbol, StringComparison.OrdinalIgnoreCase)) ?? new StockQuote(s.Symbol, s.Name, 0m, 0m, 0m, s.Market == "JP" ? "JPY" : "USD")).ToList();
            return ordered;
        }
        catch
        {
            return symbols.Select(s => new StockQuote(s.Symbol, s.Name, 0m, 0m, 0m, s.Market == "JP" ? "JPY" : "USD")).ToList();
        }
    }
}
