namespace QQJevHud.Core;

/// <summary>
/// Built-in reply tones ("话术") plus user-defined ones. A tone is a name + the writing
/// instructions the generator follows, so one message can be answered in several voices and the
/// user picks what fits the moment.
/// </summary>
public static class ToneCatalog
{
    public static readonly IReadOnlyList<Tone> BuiltIn = new[]
    {
        new Tone("高情商话术", "先接住对方情绪再回应，得体、不卑不亢，简短自然。"),
        new Tone("自然接话", "像平时聊天一样自然接上话题，简短、口语化，不刻意。"),
        new Tone("温柔体贴", "先回应对方感受，再顺着话题说，温和、有分寸，不说教。"),
        new Tone("稳如老狗", "情绪稳定、就事论事，不急不躁，四平八稳。"),
        new Tone("拒绝加班", "礼貌而坚定地表达边界，婉拒不合理要求，不内疚。"),
        new Tone("卑微乙方", "客气周到、留有余地，把姿态放软但守住底线。"),
        new Tone("职场黑话", "用职场套话专业地回应，抓重点、给方案、会总结。"),
        new Tone("理科直男", "直接、就事论事、给结论和依据，不绕弯子。"),
        new Tone("阴阳怪气", "略带反讽和幽默地回应，点到为止，不撕破脸。"),
        new Tone("已读乱回", "轻松插科打诨、出其不意地接话，活跃气氛。"),
    };

    /// <summary>Parses user-defined tones from a "名字=说明|名字=说明" string.</summary>
    public static IReadOnlyList<Tone> ParseCustom(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<Tone>();
        return raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => entry.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2 && parts[0].Length > 0)
            .Select(parts => new Tone(parts[0], parts[1]))
            .ToArray();
    }

    /// <summary>Built-in tones followed by any custom ones (custom wins on a name clash).</summary>
    public static IReadOnlyList<Tone> All(string? customRaw)
    {
        var custom = ParseCustom(customRaw);
        if (custom.Count == 0) return BuiltIn;
        var map = BuiltIn.ToDictionary(t => t.Name, t => t);
        foreach (var tone in custom) map[tone.Name] = tone;
        return map.Values.ToArray();
    }
}
