using QQJevHud.Core;

namespace QQJevHud.Tests;

public sealed class ToneAndReplyTests
{
    [Fact]
    public void ToneCatalog_HasTheBuiltInStyles()
    {
        var names = ToneCatalog.BuiltIn.Select(tone => tone.Name).ToArray();

        Assert.Contains("高情商话术", names);
        Assert.Contains("拒绝加班", names);
        Assert.Contains("阴阳怪气", names);
    }

    [Fact]
    public void ParseCustom_ParsesNameEqualsDescriptionPairs()
    {
        var tones = ToneCatalog.ParseCustom("自然接话=像平时聊天一样|温柔一点=先回应感受");

        Assert.Equal(2, tones.Count);
        Assert.Equal("自然接话", tones[0].Name);
        Assert.Equal("先回应感受", tones[1].Description);
    }

    [Fact]
    public void ResolveTones_SelectsConfiguredNamesAndFallsBack()
    {
        var settings = new AppSettings { SelectedTones = "稳如老狗, 职场黑话" };

        var tones = settings.ResolveTones();

        Assert.Equal(2, tones.Count);
        Assert.Equal("稳如老狗", tones[0].Name);
        Assert.Equal("职场黑话", tones[1].Name);

        var fallback = new AppSettings { SelectedTones = "不存在的名字" }.ResolveTones();
        Assert.Equal(3, fallback.Count);   // falls back to the first three built-ins
    }

    [Fact]
    public void ParseDraft_ReadsGroupedCandidates()
    {
        var tones = new[] { new Tone("高情商话术", "得体") };
        var content = """
        {"groups":[{"tone":"高情商话术","replies":[{"style":"稳妥","text":"我在的，刚刚在忙，现在看。"},{"style":"有个性","text":"刚在忙，这不就来了嘛。"}]}]}
        """;

        var draft = OpenAiReplyGenerator.ParseDraft("m1", "你在吗", content, tones);

        Assert.Single(draft.Groups);
        Assert.Equal(2, draft.Groups[0].Candidates.Count);
        Assert.Equal("稳妥", draft.Groups[0].Candidates[0].Style);
        Assert.Contains("现在看", draft.Groups[0].Candidates[0].Text);
    }

    [Fact]
    public void ParseDraft_ToleratesCodeFencesAndGarbage()
    {
        var tones = new[] { new Tone("自然接话", "自然") };
        var fenced = "```json\n{\"groups\":[{\"tone\":\"自然接话\",\"replies\":[{\"text\":\"哈哈好\"}]}]}\n```";

        var draft = OpenAiReplyGenerator.ParseDraft("m1", "x", fenced, tones);

        Assert.Single(draft.Groups);
        Assert.Equal("哈哈好", draft.Groups[0].Candidates[0].Text);

        var broken = OpenAiReplyGenerator.ParseDraft("m2", "x", "not json", tones);
        Assert.Empty(broken.Groups);
    }
}
