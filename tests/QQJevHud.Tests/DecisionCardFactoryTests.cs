using System.Text.Json;
using QQJevHud.Core;

namespace QQJevHud.Tests;

public sealed class DecisionCardFactoryTests
{
    [Fact]
    public void ToRiskLevel_MapsZeroTwoScoreOntoOneToTen()
    {
        Assert.Equal(1, DecisionCardFactory.ToRiskLevel(0));
        Assert.Equal(5, DecisionCardFactory.ToRiskLevel(1));
        Assert.Equal(10, DecisionCardFactory.ToRiskLevel(2));
    }

    [Theory]
    [InlineData(8, "高风险")]
    [InlineData(5, "需要留意")]
    [InlineData(3, "低风险")]
    public void RiskLabel_UsesThresholds(int level, string expected) =>
        Assert.Equal(expected, DecisionCardFactory.RiskLabel(level));

    [Fact]
    public void Build_ComposesCardWithSharedTopicAndAction()
    {
        var options = new[] { new DecisionOption("a", 60), new DecisionOption("b", 40) };

        var card = DecisionCardFactory.Build("m1", options, 2, needsResponse: true, confidence: 80);

        Assert.Equal("对方真实意图", card.Topic);
        Assert.Equal(2, card.RiskLevel);
        Assert.Equal("低风险", card.RiskLabel);
        Assert.Equal("认真回应重点，不要立刻转移话题", card.RecommendedAction);
        Assert.Equal(AnalysisState.Ready, card.State);
    }

    [Fact]
    public void ReadOptions_RanksProbabilitiesAndCapsAtFour()
    {
        using var document = JsonDocument.Parse("""{"probabilities":{"a":0.1,"b":0.5,"c":0.2,"d":0.15,"e":0.05}}""");

        var options = DecisionCardFactory.ReadOptions(document.RootElement);

        Assert.Equal(4, options.Count);
        Assert.Equal("b", options[0].Label);
        Assert.Equal(50, options[0].Probability);
    }
}
