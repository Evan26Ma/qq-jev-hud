using System.Text.Json;
using QQJevHud.Core;

namespace QQJevHud.Services;

/// <summary>
/// Persists non-sensitive settings to <c>%LOCALAPPDATA%\QQJevHud\settings.json</c>.
/// Mirrors <see cref="CalibrationStore"/>: tolerant load (defaults on any error), never stores keys.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _path;

    /// <summary>Optional <paramref name="path"/> is for tests; production uses the default location.</summary>
    public SettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QQJevHud", "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions)?.Normalize() ?? AppSettings.Default
                : AppSettings.Default;
        }
        catch
        {
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
