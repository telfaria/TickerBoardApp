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
    { _settings = await _settingsService.LoadAsync(); ApplyWindowSettings(); await _viewModel.InitializeAsync(_settings.Symbols, _settings.RefreshIntervalSeconds); }
    private void ApplyWindowSettings()
    { var screen = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == _settings.DisplayDeviceName) ?? Forms.Screen.PrimaryScreen!; _settings.DisplayDeviceName ??= screen.DeviceName; var bounds = screen.Bounds; Left = bounds.Left; Top = bounds.Top; Width = bounds.Width; Height = _settings.Height; Opacity = Math.Clamp(_settings.OpacityPercent / 100d, 0.2, 1.0); Topmost = _settings.AlwaysOnTop; }
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    { if (e.ClickCount == 2) { OpenSettingsMenu(); return; } DragMove(); var screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top)); _settings.DisplayDeviceName = screen.DeviceName; ApplyWindowSettings(); _ = _settingsService.SaveAsync(_settings); }
    private void OnSettingsClick(object sender, MouseButtonEventArgs e) { e.Handled = true; OpenSettingsMenu(); }
    private void OpenSettingsMenu()
    { var menu = new ContextMenu(); var displays = new MenuItem { Header = "表示ディスプレイ" }; foreach (var screen in Forms.Screen.AllScreens) { var item = new MenuItem { Header = $"{screen.DeviceName} ({screen.Bounds.Width} × {screen.Bounds.Height})", IsCheckable = true, IsChecked = screen.DeviceName == _settings.DisplayDeviceName }; item.Click += async (_, _) => { _settings.DisplayDeviceName = screen.DeviceName; await _settingsService.SaveAsync(_settings); ApplyWindowSettings(); }; displays.Items.Add(item); } var topmost = new MenuItem { Header = "常に手前に表示", IsCheckable = true, IsChecked = _settings.AlwaysOnTop }; topmost.Click += async (_, _) => { _settings.AlwaysOnTop = topmost.IsChecked; await _settingsService.SaveAsync(_settings); Topmost = _settings.AlwaysOnTop; }; var exit = new MenuItem { Header = "終了" }; exit.Click += (_, _) => Close(); menu.Items.Add(displays); menu.Items.Add(topmost); menu.Items.Add(new Separator()); menu.Items.Add(exit); menu.IsOpen = true; }
}
