using System.Text.Json;
using QQJevHud.Core;

namespace QQJevHud.Tests;

public sealed class RichCardTests
{
    [Fact]
    public void Dimension_ReadsChoiceAnswerIntoLabeledDimension()
    {
        using var document = JsonDocument.Parse("""
        {"intent":{"type":"choice","choice":"表达情绪","probabilities":{"希望具体回应":0.15,"表达情绪":0.6},"confidence":0.8}}
        """);

        var dimension = DecisionCardFactory.Dimension(document.RootElement, "intent", "真实意图", highlight: true);

        Assert.NotNull(dimension);
        Assert.Equal("真实意图", dimension!.Label);
        Assert.True(dimension.Highlight);
        Assert.Equal("表达情绪", dimension.Options[0].Label);
        Assert.Equal(60, dimension.Options[0].Probability);
    }

    [Fact]
    public void ScoreDimension_NamesLevelsFromLegend()
    {
        using var document = JsonDocument.Parse("""
        {"tone_distance":{"type":"score","score":3.0,"legend":{"0":"客气疏远","1":"亲近随意"},"probabilities":{"0":0.2,"1":0.8}}}
        """);

        var dimension = DecisionCardFactory.ScoreDimension(document.RootElement, "tone_distance", "语气亲疏");

        Assert.NotNull(dimension);
        Assert.Equal("亲近随意", dimension!.Options[0].Label);
        Assert.Equal(80, dimension.Options[0].Probability);
    }

    [Fact]
    public void Dimensions_DropMissingAnswersAndKeepOrder()
    {
        using var document = JsonDocument.Parse("""{"intent":{"probabilities":{"a":1.0}}}""");

        var dimensions = DecisionCardFactory.Dimensions(
            ("潜台词", DecisionCardFactory.Dimension(document.RootElement, "subtext", "潜台词")),
            ("真实意图", DecisionCardFactory.Dimension(document.RootElement, "intent", "真实意图", highlight: true)));

        Assert.Single(dimensions);
        Assert.Equal("真实意图", dimensions[0].Label);
    }

    [Fact]
    public void VisibleDimensions_PutHighlightedFirstAndCap()
    {
        var card = new DecisionCard("m", "t", "q", Array.Empty<DecisionOption>(), 3, "低风险", "a", 70, AnalysisState.Ready)
        {
            Dimensions = new[]
            {
                new JudgmentDimension("普通", new[] { new DecisionOption("x", 50) }),
                new JudgmentDimension("重要", new[] { new DecisionOption("y", 80) }, Highlight: true)
            }
        };

        var visible = card.VisibleDimensions();

        Assert.Equal("重要", visible[0].Label);
    }

    [Fact]
    public void CardMetrics_TallerWhenDimensionsPresent()
    {
        var plain = new DecisionCard("m", "t", "q", Array.Empty<DecisionOption>(), 3, "低风险", "a", 70, AnalysisState.Ready);
        var rich = plain with { Dimensions = new[] { new JudgmentDimension("意图", new[] { new DecisionOption("x", 50) }) } };

        Assert.Equal(CardMetrics.BaseHeight + CardMetrics.QuoteHeight, CardMetrics.HeightFor(plain));
        Assert.True(CardMetrics.HeightFor(rich) > CardMetrics.HeightFor(plain));
    }

    [Fact]
    public async Task MockProvider_ProducesRichDimensions()
    {
        var message = new ChatMessage("m1", "s1", MessageDirection.Incoming, "你今天怎么没理我？", 0.9, new ScreenRect());
        var card = await new MockDecisionProvider().AnalyzeAsync(message, Array.Empty<ChatMessage>(), CancellationToken.None);

        Assert.NotEmpty(card.Dimensions);
        Assert.Contains(card.Dimensions, dimension => dimension.Highlight);
        Assert.All(card.Dimensions.SelectMany(dimension => dimension.Options), option => Assert.InRange(option.Probability, 0, 100));
    }
}
