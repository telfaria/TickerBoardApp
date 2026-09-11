using System.Windows;
using System.Windows.Input;
using Forms = System.Windows.Forms;
namespace TickerBoard;
public partial class MainWindow : Window
{
    private readonly JsonSettingsService _settingsService;
    private readonly MainViewModel _viewModel;
    private AppSettings _settings = new();
    public MainWindow(JsonSettingsService settingsService, IMarketDataProvider marketDataProvider)
    { _settingsService = settingsService; _viewModel = new MainViewModel(marketDataProvider); DataContext = _viewModel; InitializeComponent(); }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _settings = await _settingsService.LoadAsync();

        var packagedDefaults = new AppSettings();
        if (_settings.Symbols is null || _settings.Symbols.Count == 0)
        {
            _settings.Symbols = packagedDefaults.Symbols;
            await _settingsService.SaveAsync(_settings);
        }

        var fontFamilyProp = typeof(AppSettings).GetProperty("FontFamily");
        if (fontFamilyProp?.GetValue(_settings) is string fontFamily && !string.IsNullOrWhiteSpace(fontFamily))
        {
            FontFamily = new System.Windows.Media.FontFamily(fontFamily);
        }

        var fontSizeProp = typeof(AppSettings).GetProperty("FontSize");
        if (fontSizeProp?.GetValue(_settings) is double fontSize && fontSize > 0)
        {
            FontSize = fontSize;
        }

        ApplyWindowSettings();
        await _viewModel.InitializeAsync(_settings.Symbols, _settings.RefreshIntervalSeconds);
    }
    private void ApplyWindowSettings()
    {
        var screen = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == _settings.DisplayDeviceName) ?? Forms.Screen.PrimaryScreen!;
        _settings.DisplayDeviceName ??= screen.DeviceName;
        var bounds = screen.Bounds; // device pixels

        // Convert device pixels to WPF device-independent units for the monitor where the window will appear.
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        var topLeft = transform.Transform(new System.Windows.Point(bounds.Left, bounds.Top));
        Left = topLeft.X;
        Top = topLeft.Y;

        // Width/Height: scale by the X/Y scale factors
        Width = bounds.Width * transform.M11;
        Height = _settings.Height; // height is user-configurable (still in WPF units)

        Opacity = Math.Clamp(_settings.OpacityPercent / 100d, 0.2, 1.0);
        Topmost = _settings.AlwaysOnTop;
    }
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { ShowSettingsWindow(); return; }
        DragMove();

        // Convert WPF window coords to device pixels to find the target screen correctly on high-DPI setups
        var source = PresentationSource.FromVisual(this);
        var toDevice = source?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
        var devicePoint = new System.Drawing.Point((int)(Left * toDevice.M11), (int)(Top * toDevice.M22));
        var screen = Forms.Screen.FromPoint(devicePoint);

        _settings.DisplayDeviceName = screen.DeviceName;
        ApplyWindowSettings();
        _ = _settingsService.SaveAsync(_settings);
    }
    private void ShowSettingsWindow()
    {
        var win = new SettingsWindow(_settingsService, _settings, app =>
        {
            // apply settings immediately
            _settings = app;
            ApplyWindowSettings();
            _ = _viewModel.InitializeAsync(_settings.Symbols, _settings.RefreshIntervalSeconds);
        });
        win.Owner = this;
        win.Show();
    }
    private void OnSettingsClick(object sender, MouseButtonEventArgs e) { e.Handled = true; OpenSettingsMenu(); }
    private void OpenSettingsMenu()
    { var menu = new System.Windows.Controls.ContextMenu(); var displays = new System.Windows.Controls.MenuItem { Header = "表示ディスプレイ" }; foreach (var screen in Forms.Screen.AllScreens) { var item = new System.Windows.Controls.MenuItem { Header = $"{screen.DeviceName} ({screen.Bounds.Width} × {screen.Bounds.Height})", IsCheckable = true, IsChecked = screen.DeviceName == _settings.DisplayDeviceName }; item.Click += async (_, _) => { _settings.DisplayDeviceName = screen.DeviceName; await _settingsService.SaveAsync(_settings); ApplyWindowSettings(); }; displays.Items.Add(item); } var topmost = new System.Windows.Controls.MenuItem { Header = "常に手前に表示", IsCheckable = true, IsChecked = _settings.AlwaysOnTop }; topmost.Click += async (_, _) => { _settings.AlwaysOnTop = topmost.IsChecked; await _settingsService.SaveAsync(_settings); Topmost = _settings.AlwaysOnTop; }; var exit = new System.Windows.Controls.MenuItem { Header = "終了" }; exit.Click += (_, _) => Close(); menu.Items.Add(displays); menu.Items.Add(topmost); menu.Items.Add(new System.Windows.Controls.Separator()); menu.Items.Add(exit); menu.IsOpen = true; }
}
