using System.IO;
using QQJevHud.Core;
using QQJevHud.Services;

namespace QQJevHud.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"qqjevhud-test-{Guid.NewGuid():N}.json");

    private SettingsStore Store() => new(_path);

    [Fact]
    public void Load_MissingFile_ReturnsDefault()
    {
        var settings = Store().Load();

        Assert.Equal(AppSettings.DefaultProvider, settings.Provider);
        Assert.Equal(AppSettings.DefaultBaseUrl, settings.BaseUrl);
        Assert.Equal(AppSettings.DefaultModel, settings.Model);
        Assert.True(settings.ShowIntent);
        Assert.True(settings.RedactSensitive);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        var store = Store();
        store.Save(new AppSettings
        {
            Provider = "openai",
            BaseUrl = "http://localhost:11434/v1",
            Model = "qwen",
            ShowRisk = false,
            CardOpacity = 0.5,
            MaxCards = 6,
            RedactSensitive = false,
            AutoStart = true
        });

        var loaded = store.Load();
        Assert.Equal("openai", loaded.Provider);
        Assert.Equal("http://localhost:11434/v1", loaded.BaseUrl);
        Assert.Equal("qwen", loaded.Model);
        Assert.False(loaded.ShowRisk);
        Assert.Equal(0.5, loaded.CardOpacity);
        Assert.Equal(6, loaded.MaxCards);
        Assert.False(loaded.RedactSensitive);
        Assert.True(loaded.AutoStart);
    }

    [Fact]
    public void Normalize_RepairsEmptyAndOutOfRangeValues()
    {
        var settings = new AppSettings
        {
            Provider = "  ",
            BaseUrl = "",
            Model = null!,
            CardOpacity = 5,
            MaxCards = 0
        }.Normalize();

        Assert.Equal(AppSettings.DefaultProvider, settings.Provider);
        Assert.Equal(AppSettings.DefaultBaseUrl, settings.BaseUrl);
        Assert.Equal(AppSettings.DefaultModel, settings.Model);
        Assert.Equal(1.0, settings.CardOpacity);
        Assert.Equal(4, settings.MaxCards);
    }

    public void Dispose()
    {
        try { File.Delete(_path); } catch { /* temp file cleanup is best-effort */ }
    }
}
