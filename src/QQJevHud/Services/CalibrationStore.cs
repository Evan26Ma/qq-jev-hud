using System.Text.Json;
using QQJevHud.Core;

namespace QQJevHud.Services;

public sealed class CalibrationStore
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QQJevHud", "calibration.json");

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
