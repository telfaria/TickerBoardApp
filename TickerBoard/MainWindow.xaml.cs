using System.Windows;
using System.Windows.Input;
using Forms = System.Windows.Forms;
namespace TickerBoard;
public partial class MainWindow : Window
{
    private readonly JsonSettingsService _settingsService;
    private readonly MainViewModel _viewModel;
    private AppSettings _settings = new();
    private System.Windows.Media.Animation.Storyboard? _tickerStoryboard;
    private System.Windows.Media.TranslateTransform? _tickerTransform;
    private double _firstContentWidth;
    public MainWindow(JsonSettingsService settingsService, IMarketDataProvider marketDataProvider)
    {
        _settingsService = settingsService;
        _viewModel = new MainViewModel(marketDataProvider);
        InitializeComponent();
        DataContext = _viewModel;
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _settings = await _settingsService.LoadAsync();

        if (_settings.Symbols is null || _settings.Symbols.Count == 0)
        {
            var defaults = await _settingsService.GetPackagedDefaultsAsync();
            _settings.Symbols = defaults.Symbols;
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

        // Debug assist: ensure ItemsControl is bound; if binding didn't pick up, assign ItemsSource explicitly
        try
        {
            var quotesControl = this.FindName("QuotesControl") as System.Windows.Controls.ItemsControl;
            if (quotesControl != null && quotesControl.ItemsSource == null)
            {
                quotesControl.ItemsSource = _viewModel.Quotes;
            }
        }
        catch { }
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
            _ = _viewModel.InitializeAsync(_settings.Symbols, _settings.RefreshIntervalSeconds).ContinueWith(_ =>
            {
                // restart ticker with new speed
                this.Dispatcher.Invoke(() => StartTickerAnimation());
            });
        });
        win.Owner = this;
        win.Show();
    }
    private void OnSettingsClick(object sender, MouseButtonEventArgs e) { e.Handled = true; OpenSettingsMenu(); }
    private void OpenSettingsMenu()
    { var menu = new System.Windows.Controls.ContextMenu(); var displays = new System.Windows.Controls.MenuItem { Header = "表示ディスプレイ" }; foreach (var screen in Forms.Screen.AllScreens) { var item = new System.Windows.Controls.MenuItem { Header = $"{screen.DeviceName} ({screen.Bounds.Width} × {screen.Bounds.Height})", IsCheckable = true, IsChecked = screen.DeviceName == _settings.DisplayDeviceName }; item.Click += async (_, _) => { _settings.DisplayDeviceName = screen.DeviceName; await _settingsService.SaveAsync(_settings); ApplyWindowSettings(); }; displays.Items.Add(item); } var topmost = new System.Windows.Controls.MenuItem { Header = "常に手前に表示", IsCheckable = true, IsChecked = _settings.AlwaysOnTop }; topmost.Click += async (_, _) => { _settings.AlwaysOnTop = topmost.IsChecked; await _settingsService.SaveAsync(_settings); Topmost = _settings.AlwaysOnTop; }; var exit = new System.Windows.Controls.MenuItem { Header = "終了" }; exit.Click += (_, _) => Close(); menu.Items.Add(displays); menu.Items.Add(topmost); menu.Items.Add(new System.Windows.Controls.Separator()); menu.Items.Add(exit); menu.IsOpen = true; }

    private void StartTickerAnimation()
    {
        try
        {
            var stack = this.FindName("TickerStack") as System.Windows.Controls.StackPanel;
            if (stack == null) return;

            if (stack.RenderTransform is System.Windows.Media.TranslateTransform tt) _tickerTransform = tt;
            else
            {
                _tickerTransform = new System.Windows.Media.TranslateTransform(0, 0);
                stack.RenderTransform = _tickerTransform;
            }

            var first = this.FindName("QuotesControl1") as System.Windows.Controls.ItemsControl;
            this.Dispatcher.InvokeAsync(() =>
            {
                _firstContentWidth = first?.ActualWidth ?? 0;
                if (_firstContentWidth <= 0 && first != null)
                {
                    // try to find the internal items panel (StackPanel) and measure it
                    var panel = FindVisualChild<System.Windows.FrameworkElement>(first);
                    if (panel != null)
                    {
                        panel.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                        _firstContentWidth = panel.DesiredSize.Width;
                    }
                }

                if (_firstContentWidth <= 0) return;

                _tickerStoryboard?.Stop();

                double speed = _settings.ScrollSpeed > 0 ? _settings.ScrollSpeed : 60.0; // px/s
                double durationSeconds = Math.Max(0.1, _firstContentWidth / speed);

                var anim = new System.Windows.Media.Animation.DoubleAnimation(0, -_firstContentWidth, TimeSpan.FromSeconds(durationSeconds))
                {
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
                };

                var sb = new System.Windows.Media.Animation.Storyboard();
                sb.Children.Add(anim);
                System.Windows.Media.Animation.Storyboard.SetTarget(anim, stack);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(anim, new System.Windows.PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

                _tickerStoryboard = sb;
                _tickerStoryboard.Begin();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
        catch { }
    }

    private static T? FindVisualChild<T>(System.Windows.DependencyObject dep) where T : System.Windows.DependencyObject
    {
        if (dep == null) return null;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(dep); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(dep, i);
            if (child is T t) return t;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}
