using System.Linq;
using System.Windows;

namespace TickerBoard;

public partial class SettingsWindow : Window
{
    private readonly JsonSettingsService _settingsService;
    private readonly SettingsViewModel _vm;
    private readonly Action<AppSettings>? _onSaved;

    public IEnumerable<string> Markets { get; } = new[] { "JP", "US", "ETF" };

    public SettingsWindow(JsonSettingsService settingsService, AppSettings current, Action<AppSettings>? onSaved)
    {
        _settingsService = settingsService;
        _vm = new SettingsViewModel(current);
        DataContext = _vm;
        _onSaved = onSaved;
        InitializeComponent();
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
