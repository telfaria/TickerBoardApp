using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace TickerBoard;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SymbolSetting> Symbols { get; } = new();
    public IList<string> Markets { get; } = new List<string> { "JP", "US", "ETF" };
    public IList<string> Fonts { get; } = new List<string>();

    private int _refreshIntervalSeconds;
    public int RefreshIntervalSeconds { get => _refreshIntervalSeconds; set => SetField(ref _refreshIntervalSeconds, value); }

    private int _height;
    public int Height { get => _height; set => SetField(ref _height, value); }

    private int _opacityPercent;
    public int OpacityPercent { get => _opacityPercent; set => SetField(ref _opacityPercent, value); }

    private bool _alwaysOnTop;
    public bool AlwaysOnTop { get => _alwaysOnTop; set => SetField(ref _alwaysOnTop, value); }

    private string _fontFamily = "Segoe UI";
    public string FontFamily { get => _fontFamily; set => SetField(ref _fontFamily, value); }

    private double _fontSize = 14.0;
    public double FontSize { get => _fontSize; set => SetField(ref _fontSize, value); }

    private int _scrollSpeed = 60;
    public int ScrollSpeed { get => _scrollSpeed; set => SetField(ref _scrollSpeed, value); }

    public SettingsViewModel(AppSettings settings)
    {
        RefreshIntervalSeconds = settings.RefreshIntervalSeconds;
        Height = settings.Height;
        OpacityPercent = settings.OpacityPercent;
        AlwaysOnTop = settings.AlwaysOnTop;
        foreach (var s in settings.Symbols) Symbols.Add(new SymbolSetting { Symbol = s.Symbol, Name = s.Name, Market = s.Market });
        FontFamily = settings.FontFamily ?? FontFamily;
        FontSize = settings.FontSize > 0 ? settings.FontSize : FontSize;
        ScrollSpeed = settings.ScrollSpeed > 0 ? settings.ScrollSpeed : ScrollSpeed;

        // populate available font family names
        foreach (var ff in System.Windows.Media.Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s)) Fonts.Add(ff);
    }

    public AppSettings ToAppSettings()
    {
        return new AppSettings
        {
            RefreshIntervalSeconds = Math.Max(10, RefreshIntervalSeconds),
            DisplayDeviceName = null,
            Height = Height,
            OpacityPercent = Math.Clamp(OpacityPercent, 20, 100),
            AlwaysOnTop = AlwaysOnTop,
            Symbols = Symbols.ToList()
        ,
            FontFamily = FontFamily,
            FontSize = FontSize,
            ScrollSpeed = ScrollSpeed
        };
    }

    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();
        if (RefreshIntervalSeconds < 10) errors.Add("更新間隔は10秒以上にしてください。");
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in Symbols)
        {
            if (string.IsNullOrWhiteSpace(s.Symbol)) { errors.Add("銘柄コードが空です。"); break; }
            if (!codes.Add(s.Symbol)) { errors.Add($"重複した銘柄コードがあります: {s.Symbol}"); break; }
        }
        if (FontSize < 8 || FontSize > 72) errors.Add("フォントサイズは8〜72の間で指定してください。");
        if (ScrollSpeed < 1 || ScrollSpeed > 1000) errors.Add("スクロール速度は1〜1000 (px/s) の範囲で指定してください。");
        return errors;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
