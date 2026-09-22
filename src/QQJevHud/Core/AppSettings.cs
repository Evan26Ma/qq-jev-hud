namespace QQJevHud.Core;

/// <summary>
/// User-configurable settings. Persisted (without any API key) to settings.json via
/// <c>Services/SettingsStore</c>. Keys live only in the Windows Credential Manager.
/// </summary>
public sealed record AppSettings
{
    // Decision service.
    public string Provider { get; set; } = DefaultProvider;   // "typesafe" | "openai" | "mock"
    public string BaseUrl { get; set; } = DefaultBaseUrl;     // OpenAI-compatible base, e.g. https://api.openai.com/v1
    public string Model { get; set; } = DefaultModel;
    public string Profile { get; set; } = DefaultProfile;     // "general" | "relationship"

    // Replies ("话我帮你想，发送你来定").
    public bool GenerateReplies { get; set; } = true;
    public string SelectedTones { get; set; } = DefaultSelectedTones;   // comma-separated tone names
    public string CustomTones { get; set; } = string.Empty;             // "名字=说明|名字=说明"

    // Card display. CardOpacity is the overlay-window opacity (1 = the designed card look).
    public bool ShowIntent { get; set; } = true;
    public bool ShowRisk { get; set; } = true;
    public bool ShowAdvice { get; set; } = true;
    public double CardOpacity { get; set; } = 1.0;
    public int MaxCards { get; set; } = 4;

    // Privacy.
    public bool RedactSensitive { get; set; } = true;

    // General.
    public bool AutoStart { get; set; }

    public const string DefaultProvider = "typesafe";
    public const string DefaultBaseUrl = "https://api.openai.com/v1";
    public const string DefaultModel = "gpt-4o-mini";
    public const string DefaultProfile = JudgmentProfiles.General;
    public const string DefaultSelectedTones = "高情商话术,自然接话,稳如老狗";

    public static AppSettings Default => new();

    public bool UsesOpenAi => string.Equals(Provider, "openai", StringComparison.OrdinalIgnoreCase);
    public bool UsesMock => string.Equals(Provider, "mock", StringComparison.OrdinalIgnoreCase);

    /// <summary>The active tones: built-in plus custom, filtered by SelectedTones (or all if empty).</summary>
    public IReadOnlyList<Tone> ResolveTones()
    {
        var all = ToneCatalog.All(CustomTones);
        var wanted = (SelectedTones ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (wanted.Length == 0) return all.Take(3).ToArray();
        var chosen = all.Where(tone => wanted.Contains(tone.Name, StringComparer.Ordinal)).ToArray();
        return chosen.Length > 0 ? chosen : all.Take(3).ToArray();
    }

    /// <summary>Repairs values that an older or partial settings file may have left empty.</summary>
    public AppSettings Normalize()
    {
        if (string.IsNullOrWhiteSpace(Provider)) Provider = DefaultProvider;
        if (string.IsNullOrWhiteSpace(BaseUrl)) BaseUrl = DefaultBaseUrl;
        if (string.IsNullOrWhiteSpace(Model)) Model = DefaultModel;
        if (string.IsNullOrWhiteSpace(SelectedTones)) SelectedTones = DefaultSelectedTones;
        CustomTones ??= string.Empty;
        if (CardOpacity <= 0 || CardOpacity > 1) CardOpacity = 1.0;
        if (MaxCards < 1 || MaxCards > 20) MaxCards = 4;
        return this;
    }
}
