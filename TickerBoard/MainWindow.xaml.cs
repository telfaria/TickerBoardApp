using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Collections.Specialized;
using Forms = System.Windows.Forms;
namespace TickerBoard;
public partial class MainWindow : Window
{
    private readonly JsonSettingsService _settingsService;
    private readonly MainViewModel _viewModel;
    private AppSettings _settings = new();
    private System.Windows.Media.TranslateTransform? _tickerTransform;
    private double _firstContentWidth;
    private System.Diagnostics.Stopwatch? _renderStopwatch;
    private double _lastRenderTime;
    private double _offset;
    private double _viewportWidth;
    private double _speedPixelsPerSecond;
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
                // Start the ticker animation after initialization
        // Delay start until after layout/first render so measurement is accurate
        this.Dispatcher.InvokeAsync(() => {
            // Allow one layout/pass to complete
            System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() => StartTickerAnimation(), System.Windows.Threading.DispatcherPriority.Loaded);
        }, System.Windows.Threading.DispatcherPriority.Background);

        // Restart/refresh animation when the quotes collection changes (items added/removed)
        try
        {
            if (_viewModel.Quotes is INotifyCollectionChanged nc)
            {
                nc.CollectionChanged -= Quotes_CollectionChanged;
                nc.CollectionChanged += Quotes_CollectionChanged;
            }
        }
        catch { }
    }

    private void Quotes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Defer to allow layout to update after items change
        this.Dispatcher.InvokeAsync(() => StartTickerAnimation(), System.Windows.Threading.DispatcherPriority.Loaded);
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
            var second = this.FindName("QuotesControl2") as System.Windows.Controls.ItemsControl;
                var grid = this.FindName("TickerGrid") as System.Windows.Controls.Grid;
            this.Dispatcher.InvokeAsync(() =>
            {
                // Try to get accurate width of the inner items panel (the StackPanel inside the ItemsControl)
                _firstContentWidth = first?.ActualWidth ?? 0;
                if ((int)_firstContentWidth == 0 && first != null)
                {
                    // Force layout update then locate the items host (StackPanel) specifically
                    first.UpdateLayout();
                    var panel = FindVisualChild<System.Windows.Controls.StackPanel>(first);
                    if (panel != null)
                    {
                        // Measure with infinite available space so DesiredSize reflects full content width
                        panel.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                        panel.UpdateLayout();

                        // Compute total width by summing child widths and horizontal margins to avoid cut-off
                        double total = 0;
                        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(panel); i++)
                        {
                            var child = System.Windows.Media.VisualTreeHelper.GetChild(panel, i) as System.Windows.FrameworkElement;
                            if (child == null) continue;
                            // Ensure child is measured
                            child.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                            var w = child.DesiredSize.Width;
                            if (child is System.Windows.FrameworkElement fe)
                            {
                                w += fe.Margin.Left + fe.Margin.Right;
                            }
                            total += w;
                        }

                        // Fallback to panel.DesiredSize if no children measured
                        _firstContentWidth = total > 0 ? total : panel.DesiredSize.Width;
                    }
                }

                if (_firstContentWidth <= 0) return;

                // Ensure the second copy matches the measured content width so the loop is seamless.
                if (second != null)
                {
                    // If measurement produced zero, use the visible window width as a safe fallback
                    var measured = _firstContentWidth > 0 ? _firstContentWidth : Math.Max(this.ActualWidth, 0);
                    second.Width = measured;
                    // Keep _firstContentWidth as the authoritative scroll cycle width
                    _firstContentWidth = Math.Max(_firstContentWidth, measured);
                }

                // Start from right edge and keep the same anchor on each loop.
                _viewportWidth = grid?.ActualWidth ?? this.ActualWidth;
                var pct = Math.Clamp(_settings.InitialOffsetPercent, 0, 100) / 100.0;
                // 100 -> first item starts at right edge, 0 -> one full cycle ahead
                _offset = (1.0 - pct) * _firstContentWidth;
                if (_firstContentWidth > 0)
                {
                    _offset = (_offset % _firstContentWidth + _firstContentWidth) % _firstContentWidth;
                }
                _tickerTransform.X = _viewportWidth - _offset;
                _speedPixelsPerSecond = _settings.ScrollSpeed > 0 ? _settings.ScrollSpeed : 60.0;

                if (_renderStopwatch == null) _renderStopwatch = System.Diagnostics.Stopwatch.StartNew();
                else _renderStopwatch.Restart();
                _lastRenderTime = _renderStopwatch.Elapsed.TotalSeconds;

                System.Windows.Media.CompositionTarget.Rendering -= OnRendering;
                System.Windows.Media.CompositionTarget.Rendering += OnRendering;
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

    private void OnRendering(object? sender, System.EventArgs e)
    {
        try
        {
            if (_tickerTransform == null) return;
            if (_firstContentWidth <= 0) return;
            if (_renderStopwatch == null) return;

            var now = _renderStopwatch.Elapsed.TotalSeconds;
            var delta = now - _lastRenderTime;
            _lastRenderTime = now;
            _offset += delta * _speedPixelsPerSecond;
            if (_firstContentWidth > 0)
            {
                while (_offset >= _firstContentWidth)
                {
                    _offset -= _firstContentWidth;
                }
                var grid = this.FindName("TickerGrid") as System.Windows.Controls.Grid;
                _viewportWidth = grid?.ActualWidth ?? this.ActualWidth;
                _tickerTransform.X = _viewportWidth - _offset;
            }
        }
        catch { }
    }

    protected override void OnClosed(System.EventArgs e)
    {
        base.OnClosed(e);
        try
        {
            System.Windows.Media.CompositionTarget.Rendering -= OnRendering;
            _renderStopwatch?.Stop();
        }
        catch { }
    }
}
