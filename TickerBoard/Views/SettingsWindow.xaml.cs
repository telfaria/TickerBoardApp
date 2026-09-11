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
