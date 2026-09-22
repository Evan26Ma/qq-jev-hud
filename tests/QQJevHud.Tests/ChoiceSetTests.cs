using QQJevHud.Core;

namespace QQJevHud.Tests;

/// <summary>
/// The choice panel is the single surface where judgment and replies meet: four options, each
/// previewing what it would lead to, safest first — like a visual novel's choice screen.
/// </summary>
public sealed class ChoiceSetTests
{
    private static DecisionCard Card() =>
        new("m1", "对方真实意图", "她此刻最希望你怎样回应？", Array.Empty<DecisionOption>(), 3, "低风险", "认真回应", 80, AnalysisState.Ready);

    private static ChoiceItem Choice(int index, string text, int reactionProbability, int riskLevel) =>
        new(index, text, "话术", new CandidateOutcome("被安抚、情绪缓和", reactionProbability, riskLevel,
            riskLevel >= 8 ? "高风险" : riskLevel >= 5 ? "需要留意" : "低风险"));

    [Fact]
    public void Ordered_PutsTheSafestBestReceivedChoiceFirst_AndRenumbers()
    {
        var set = new ChoiceSet("m1", "消息", "零", Card(), new[]
        {
            Choice(1, "冒险的说法", 40, 5),
            Choice(2, "稳妥的说法", 60, 2),
            Choice(3, "更好的说法", 70, 1)
        });

        var ordered = set.Ordered;

        Assert.Equal(3, ordered.Count);
        Assert.Equal("更好的说法", ordered[0].Text);
        Assert.Equal("稳妥的说法", ordered[1].Text);
        Assert.Equal("冒险的说法", ordered[2].Text);
        // Renumbered 1..n for display.
        Assert.Equal(new[] { 1, 2, 3 }, ordered.Select(choice => choice.Index));
    }

    [Fact]
    public void Rank_PrefersLowerRiskOverHigherProbability()
    {
        var risky = Choice(1, "高风险高概率", 90, 8);
        var safe = Choice(2, "低风险低概率", 40, 1);

        Assert.True(safe.Rank > risky.Rank);
    }

    [Fact]
    public void Rank_PrefersAGoodReactionOverAMerelyLikelyBadOne()
    {
        var badButLikely = new ChoiceItem(1, "会被敷衍", "话术",
            new CandidateOutcome("觉得被敷衍", 70, 2, "低风险"));
        var goodButLessLikely = new ChoiceItem(2, "会被安抚", "话术",
            new CandidateOutcome("被安抚、情绪缓和", 45, 2, "低风险"));

        Assert.True(goodButLessLikely.Rank > badButLikely.Rank);
    }

    [Fact]
    public void Ordered_ShowsAGoodReactionFirst_EvenIfABadOneIsMoreLikely()
    {
        var set = new ChoiceSet("m1", "消息", null, Card(), new[]
        {
            new ChoiceItem(1, "会被敷衍但概率高", "话术", new CandidateOutcome("觉得被敷衍", 70, 2, "低风险")),
            new ChoiceItem(2, "会被安抚概率低些", "话术", new CandidateOutcome("被安抚、情绪缓和", 45, 2, "低风险"))
        });

        Assert.Equal("会被安抚概率低些", set.Ordered[0].Text);
    }

    [Fact]
    public void Rank_IsZero_WhenJevCouldNotEvaluate()
    {
        var unevaluated = new ChoiceItem(1, "没有预测的候选", "话术", null);

        Assert.Equal(0, unevaluated.Rank);
    }

    [Fact]
    public void Ordered_KeepsUnevaluatedChoicesAtTheEnd()
    {
        var set = new ChoiceSet("m1", "消息", null, Card(), new[]
        {
            new ChoiceItem(1, "无预测", "话术", null),
            Choice(2, "有预测", 50, 2)
        });

        var ordered = set.Ordered;

        Assert.Equal("有预测", ordered[0].Text);
        Assert.Equal("无预测", ordered[1].Text);
    }

    [Fact]
    public void ChoiceSet_CarriesSenderAndMessageForThePanelHeader()
    {
        var set = new ChoiceSet("m1", "我昨天买的M7五级弹", "零", Card(), Array.Empty<ChoiceItem>());

        Assert.Equal("零", set.Sender);
        Assert.Equal("我昨天买的M7五级弹", set.MessageText);
        Assert.Empty(set.Choices);
    }
}
