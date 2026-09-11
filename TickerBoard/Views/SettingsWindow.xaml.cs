using System.Linq;
using System.Windows;

namespace TickerBoard;

public partial class SettingsWindow : Window
{
    private readonly JsonSettingsService _settingsService;
    private readonly SettingsViewModel _vm;
    private readonly Action<AppSettings>? _onSaved;

    // Markets are provided by the ViewModel; keep this for compatibility if needed
    public IEnumerable<string> Markets => _vm.Markets;

    public SettingsWindow(JsonSettingsService settingsService, AppSettings current, Action<AppSettings>? onSaved)
    {
        _settingsService = settingsService;
        _vm = new SettingsViewModel(current);
        DataContext = _vm;
        _onSaved = onSaved;
        InitializeComponent();

        // Ensure Save/Cancel buttons are docked at bottom and do not get overlapped by the settings area.
        try
        {
            var root = this.Content as System.Windows.Controls.DockPanel;
            var saveBtn = this.FindName("SaveButton") as System.Windows.Controls.Button;
            var dataGrid = this.FindName("SymbolsGrid") as System.Windows.Controls.DataGrid;
            if (root != null && saveBtn != null && dataGrid != null)
            {
                // Remove and re-insert Save button right after the DataGrid so DockPanel will layout correctly
                root.Children.Remove(saveBtn);
                var idx = root.Children.IndexOf(dataGrid);
                if (idx >= 0) root.Children.Insert(idx + 1, saveBtn);
                System.Windows.Controls.DockPanel.SetDock(saveBtn, System.Windows.Controls.Dock.Bottom);
            }
        }
        catch { /* ignore any rearrange errors */ }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        var errors = _vm.Validate().ToList();
        if (errors.Any())
        {
            System.Windows.MessageBox.Show(string.Join("\n", errors), "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Before saving, resolve display names and markets by calling the market data provider.
        var provider = new YahooMarketDataProvider();
        // For each entered symbol, try to resolve its name and market. Prefer JP (.T) first to get Japanese names, then US
        var symbols = _vm.Symbols.ToList();
        foreach (var s in symbols)
        {
            var code = (s.Symbol ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(code)) continue;
            // Try JP (Tokyo) first to prefer Japanese names
            var tryJp = await provider.GetQuotesAsync(new[] { new SymbolSetting { Symbol = code, Name = string.Empty, Market = "JP" } });
            var quoteJ = tryJp.FirstOrDefault();
            if (quoteJ != null && !string.IsNullOrWhiteSpace(quoteJ.Name) && quoteJ.Price != 0m)
            {
                s.Name = quoteJ.Name;
                s.Market = "JP";
                continue;
            }

            // Then try US
            var tryUs = await provider.GetQuotesAsync(new[] { new SymbolSetting { Symbol = code, Name = string.Empty, Market = "US" } });
            var quote = tryUs.FirstOrDefault();
            if (quote != null && !string.IsNullOrWhiteSpace(quote.Name) && quote.Price != 0m)
            {
                s.Name = quote.Name;
                s.Market = "US";
                continue;
            }

            // Fallback: use returned name if any, else keep existing name or code
            if (quoteJ != null && !string.IsNullOrWhiteSpace(quoteJ.Name)) s.Name = quoteJ.Name;
            else if (quote != null && !string.IsNullOrWhiteSpace(quote.Name)) s.Name = quote.Name;
            else if (string.IsNullOrWhiteSpace(s.Name)) s.Name = code;
            s.Market ??= "US";
        }

        var app = _vm.ToAppSettings();
        // Preserve DisplayDeviceName if present
        var existing = await _settingsService.LoadAsync();
        app.DisplayDeviceName = existing.DisplayDeviceName;

        await _settingsService.SaveAsync(app);
        _onSaved?.Invoke(app);
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
