using QQJevHud.Core;

namespace QQJevHud.Tests;

public sealed class OpenAiDecisionProviderTests
{
    [Fact]
    public void ParseJudgment_MapsModelJsonToDecisionCard()
    {
        var content = """
        {"intent":{"label":"表达情绪","probabilities":{"希望具体回应":0.15,"确认被在意":0.1,"表达情绪":0.6,"自然分享":0.15},"confidence":0.7},
         "relationship_risk":{"score":1.0},
         "needs_response":{"value":0.8}}
        """;

        var card = OpenAiDecisionProvider.ParseJudgment("m1", content);

        Assert.Equal(AnalysisState.Ready, card.State);
        Assert.Equal(70, card.Confidence);
        Assert.Equal(5, card.RiskLevel);                          // score 1.0 on 0..2 -> 5/10
        Assert.Equal("需要留意", card.RiskLabel);
        Assert.Equal("认真回应重点，不要立刻转移话题", card.RecommendedAction);
        Assert.Equal("表达情绪", card.Options[0].Label);
        Assert.Equal(60, card.Options[0].Probability);
    }

    [Fact]
    public void ParseJudgment_ToleratesCodeFencesAndBareNumbers()
    {
        var content = "```json\n{\"intent\":{\"probabilities\":{\"a\":1.0},\"confidence\":0.9},\"relationship_risk\":2,\"needs_response\":0}\n```";

        var card = OpenAiDecisionProvider.ParseJudgment("m2", content);

        Assert.Equal(10, card.RiskLevel);                         // bare 2 -> 10/10
        Assert.Equal("高风险", card.RiskLabel);
        Assert.Equal("自然回应并顺着话题继续", card.RecommendedAction);
        Assert.Equal(90, card.Confidence);
    }

    [Fact]
    public void ParseJudgment_Unparseable_ReturnsFailed()
    {
        var card = OpenAiDecisionProvider.ParseJudgment("m3", "not json at all");

        Assert.Equal(AnalysisState.Failed, card.State);
    }
}
