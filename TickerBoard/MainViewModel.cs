using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Threading;
namespace TickerBoard;
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IMarketDataProvider _marketDataProvider; private readonly DispatcherTimer _timer = new(); private IReadOnlyList<SymbolSetting> _symbols = []; private string _lastUpdatedText = "更新待ち"; private bool _isUpdating;
    public ObservableCollection<QuoteViewModel> Quotes { get; } = [];
    public string LastUpdatedText { get => _lastUpdatedText; private set => SetField(ref _lastUpdatedText, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    public MainViewModel(IMarketDataProvider marketDataProvider) { _marketDataProvider = marketDataProvider; _timer.Tick += async (_, _) => await RefreshAsync(); }
    public async Task InitializeAsync(IReadOnlyList<SymbolSetting> symbols, int refreshIntervalSeconds) { _symbols = symbols; _timer.Interval = TimeSpan.FromSeconds(Math.Max(10, refreshIntervalSeconds)); await RefreshAsync(); _timer.Start(); }
    private async Task RefreshAsync()
    {
        if (_isUpdating) return; _isUpdating = true;
        try { var quotes = await _marketDataProvider.GetQuotesAsync(_symbols); Quotes.Clear(); foreach (var quote in quotes) Quotes.Add(new QuoteViewModel(quote)); LastUpdatedText = $"更新 {DateTime.Now:HH:mm}"; }
        catch (Exception) { LastUpdatedText = "データの更新に失敗しました"; }
        finally { _isUpdating = false; }
    }
    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return; field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
}
public sealed class QuoteViewModel(StockQuote quote)
{
    public string Symbol => quote.Symbol; public string Name => quote.Name;
    public string PriceDisplay => quote.Currency == "JPY" ? quote.Price.ToString("N2", CultureInfo.InvariantCulture) : "$" + quote.Price.ToString("N2", CultureInfo.InvariantCulture);
    public string ChangeDisplay => $"{quote.Change:+0.00;-0.00;0.00} ({quote.ChangePercent:+0.00;-0.00;0.00}%)";
public System.Windows.Media.Brush ChangeBrush => quote.Change > 0 ? System.Windows.Media.Brushes.LimeGreen : quote.Change < 0 ? System.Windows.Media.Brushes.OrangeRed : System.Windows.Media.Brushes.LightGray;
}
