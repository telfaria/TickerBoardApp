using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TickerBoard;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SymbolSetting> Symbols { get; } = new();

    private int _refreshIntervalSeconds;
    public int RefreshIntervalSeconds { get => _refreshIntervalSeconds; set => SetField(ref _refreshIntervalSeconds, value); }

    private int _height;
    public int Height { get => _height; set => SetField(ref _height, value); }

    private int _opacityPercent;
    public int OpacityPercent { get => _opacityPercent; set => SetField(ref _opacityPercent, value); }

    private bool _alwaysOnTop;
    public bool AlwaysOnTop { get => _alwaysOnTop; set => SetField(ref _alwaysOnTop, value); }

    public SettingsViewModel(AppSettings settings)
    {
        RefreshIntervalSeconds = settings.RefreshIntervalSeconds;
        Height = settings.Height;
        OpacityPercent = settings.OpacityPercent;
        AlwaysOnTop = settings.AlwaysOnTop;
        foreach (var s in settings.Symbols) Symbols.Add(new SymbolSetting { Symbol = s.Symbol, Name = s.Name, Market = s.Market });
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
            if (string.IsNullOrWhiteSpace(s.Market)) { errors.Add($"市場が未選択の銘柄があります: {s.Symbol}"); break; }
        }
        return errors;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
