namespace QQJevHud.Services;

/// <summary>Records only anonymous runtime state codes; chat content and screenshots are never written.</summary>
internal static class HudDiagnostics
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QQJevHud", "runtime.log");

    public static void Record(string state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTimeOffset.UtcNow:O}\t{state}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must never affect the overlay.
        }
    }
}
