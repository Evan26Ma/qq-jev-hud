using System.Text.Json;
using QQJevHud.Core;

namespace QQJevHud.Services;

public sealed class CalibrationStore
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QQJevHud", "calibration.json");

    /// <summary>True once the user has calibrated by hand; the region is then never auto-detected.</summary>
    public bool IsUserConfigured => File.Exists(_path);

    public Calibration Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<Calibration>(File.ReadAllText(_path)) ?? Calibration.Default
                : Calibration.Default;
        }
        catch { return Calibration.Default; }
    }

    public void Save(Calibration calibration)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(calibration));
    }
}
