namespace TickerBoard;
public sealed record StockQuote(string Symbol, string Name, decimal Price, decimal Change, decimal ChangePercent, string Currency);
public interface IMarketDataProvider { Task<IReadOnlyList<StockQuote>> GetQuotesAsync(IReadOnlyList<SymbolSetting> symbols, CancellationToken cancellationToken = default); }
public sealed class MockMarketDataProvider : IMarketDataProvider
{
    private readonly Random _random = new();
    private readonly Dictionary<string, decimal> _prices = new() { ["7203"] = 2847.50m, ["9432"] = 168.20m, ["AAPL"] = 228.50m, ["NVDA"] = 174.25m, ["SPY"] = 649.50m };
    public Task<IReadOnlyList<StockQuote>> GetQuotesAsync(IReadOnlyList<SymbolSetting> symbols, CancellationToken cancellationToken = default)
    {
        var quotes = symbols.Select(symbol => { var price = _prices.TryGetValue(symbol.Symbol, out var saved) ? saved : 100m; var percent = Math.Round((decimal)(_random.NextDouble() * 2 - 1), 2); var change = Math.Round(price * percent / 100m, 2); price = Math.Round(price + change, 2); _prices[symbol.Symbol] = price; return new StockQuote(symbol.Symbol, symbol.Name, price, change, percent, symbol.Market == "JP" ? "JPY" : "USD"); }).ToList();
        return Task.FromResult<IReadOnlyList<StockQuote>>(quotes);
    }
}
