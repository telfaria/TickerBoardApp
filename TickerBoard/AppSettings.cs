namespace TickerBoard;
public sealed class AppSettings
{
    public int RefreshIntervalSeconds { get; set; } = 60;
    public string? DisplayDeviceName { get; set; }
    public int Height { get; set; } = 58;
    public int OpacityPercent { get; set; } = 96;
    public bool AlwaysOnTop { get; set; } = true;
    public List<SymbolSetting> Symbols { get; set; } = [];
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 14.0;
    public double ScrollSpeed { get; set; } = 60.0; // pixels per second
}
public sealed class SymbolSetting
{
    public required string Symbol { get; set; }
    public required string Name { get; set; }
    public required string Market { get; set; }
}
