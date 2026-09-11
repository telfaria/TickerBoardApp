using System.Collections.Generic;
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

        // Batch resolve display names and markets by calling the market data provider.
        var provider = new YahooMarketDataProvider();
        var symbols = _vm.Symbols.ToList();
        var codes = symbols
            .Select(s => (s.Symbol ?? string.Empty).Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resolved = new Dictionary<string, (string Name, string Market)>(StringComparer.OrdinalIgnoreCase);
        if (codes.Count > 0)
        {
            // Try JP first in one batch to prefer Japanese names.
            var jpRequests = codes
                .Select(code => new SymbolSetting { Symbol = code, Name = string.Empty, Market = "JP" })
                .ToList();
            var jpResults = (await provider.GetQuotesAsync(jpRequests)).ToList();
            for (int i = 0; i < jpRequests.Count; i++)
            {
                var quote = jpResults.ElementAtOrDefault(i);
                if (quote != null && !string.IsNullOrWhiteSpace(quote.Name) && quote.Price != 0m)
                {
                    resolved[jpRequests[i].Symbol] = (quote.Name, "JP");
                }
            }

            // Query unresolved codes in one US batch.
            var unresolved = codes.Where(code => !resolved.ContainsKey(code)).ToList();
            if (unresolved.Count > 0)
            {
                var usRequests = unresolved
                    .Select(code => new SymbolSetting { Symbol = code, Name = string.Empty, Market = "US" })
                    .ToList();
                var usResults = (await provider.GetQuotesAsync(usRequests)).ToList();
                for (int i = 0; i < usRequests.Count; i++)
                {
                    var quote = usResults.ElementAtOrDefault(i);
                    if (quote != null && !string.IsNullOrWhiteSpace(quote.Name) && quote.Price != 0m)
                    {
                        resolved[usRequests[i].Symbol] = (quote.Name, "US");
                    }
                }
            }
        }

        // Apply resolved names/markets back to settings symbols.
        foreach (var s in symbols)
        {
            var code = (s.Symbol ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(code)) continue;

            if (resolved.TryGetValue(code, out var info))
            {
                s.Name = string.IsNullOrWhiteSpace(info.Name) ? code : info.Name;
                s.Market = info.Market;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(s.Name)) s.Name = code;
                s.Market ??= "US";
            }
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
