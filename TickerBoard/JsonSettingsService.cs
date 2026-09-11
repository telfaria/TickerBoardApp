using System;
using System.IO;
using System.Text.Json;
namespace TickerBoard;
public sealed class JsonSettingsService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TickerBoard", "settings.json");
    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_settingsPath)) { var defaults = await LoadPackagedDefaultsAsync(); await SaveAsync(defaults); return defaults; }
        await using var stream = File.OpenRead(_settingsPath);
        return await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options) ?? new AppSettings();
    }
    public async Task<AppSettings> GetPackagedDefaultsAsync() => await LoadPackagedDefaultsAsync();
    public async Task SaveAsync(AppSettings settings)
    { Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!); await using var stream = File.Create(_settingsPath); await JsonSerializer.SerializeAsync(stream, settings, Options); }
    private static async Task<AppSettings> LoadPackagedDefaultsAsync()
    { await using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "appsettings.json")); return (await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options)) ?? new AppSettings(); }
}
