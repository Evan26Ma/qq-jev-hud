using System.Text.Json;
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
    public void ParseCandidates_ReadsToneAndText()
    {
        var tones = new[] { new Tone("高情商话术", "得体") };
        var content = """
        {"choices":[{"tone":"高情商话术","text":"我在的，刚刚在忙，现在看。"},{"tone":"理科直男","text":"刚在处理事情，你说。"}]}
        """;

        var candidates = OpenAiReplyGenerator.ParseCandidates(content, tones);

        Assert.Equal(2, candidates.Count);
        Assert.Equal("高情商话术", candidates[0].ToneName);
        Assert.Contains("现在看", candidates[0].Text);
        Assert.Equal("理科直男", candidates[1].ToneName);
    }

    [Fact]
    public void ParseCandidates_ToleratesCodeFencesAndGarbage()
    {
        var tones = new[] { new Tone("自然接话", "自然") };
        var fenced = "```json\n{\"choices\":[{\"text\":\"哈哈好\"}]}\n```";

        var candidates = OpenAiReplyGenerator.ParseCandidates(fenced, tones);

        Assert.Single(candidates);
        Assert.Equal("哈哈好", candidates[0].Text);
        Assert.Equal("自然接话", candidates[0].ToneName);   // tone falls back to the first configured tone

        Assert.Empty(OpenAiReplyGenerator.ParseCandidates("not json", tones));
    }

    [Fact]
    public void MaxChoices_IsFour()
    {
        Assert.Equal(4, OpenAiReplyGenerator.MaxChoices);
    }
}
