using QQJevHud.Core;
using QQJevHud.Views;

namespace QQJevHud.Tests;

/// <summary>
/// The replies live inside the card: it starts collapsed and shows a hint, expands to four selectable
/// rows once Jev has ranked them, and grows to fit.
/// </summary>
public sealed class CardExpansionTests
{
    private static DecisionCard Card() =>
        new("m1", "对方真实意图", "她此刻最希望你怎样回应？", Array.Empty<DecisionOption>(), 2, "低风险", "轻松接话", 80, AnalysisState.Ready);

    private static ChoiceSet Set(params ChoiceItem[] choices) =>
        new("m1", "我昨天买的M7五级弹", "零", Card(), choices);

    private static ChoiceItem Choice(int index, string text, int risk) =>
        new(index, text, "话术", new CandidateOutcome("被安抚、情绪缓和", 60, risk, risk >= 5 ? "需要留意" : "低风险"));

    private static OverlayCardViewModel NewCard() => new()
    {
        Card = Card(),
        MessageText = "我昨天买的M7五级弹",
        Sender = "零",
        BaseHeight = CardMetrics.HeightFor(Card())
    };

    [Fact]
    public void NewCard_StartsCollapsedWithoutChoices()
    {
        var card = NewCard();

        Assert.False(card.HasChoices);
        Assert.False(card.IsExpanded);
        Assert.False(card.ShowChoices);
        Assert.Equal(CardMetrics.HeightFor(Card()), card.Height);
    }

    [Fact]
    public void ApplyChoices_FillsRowsButStaysCollapsedUntilClicked()
    {
        var card = NewCard();

        card.ApplyChoices(Set(Choice(1, "第一句", 2), Choice(2, "第二句", 5)));

        Assert.True(card.HasChoices);
        Assert.Equal(2, card.ChoiceRows.Count);
        Assert.False(card.ShowChoices);                       // still collapsed
        Assert.Equal("怎么回 ▾", card.ExpandHint);
    }

    [Fact]
    public void Expanding_GrowsTheCardAndShowsTheRows()
    {
        var card = NewCard();
        card.ApplyChoices(Set(Choice(1, "第一句", 2), Choice(2, "第二句", 5), Choice(3, "第三句", 2)));
        var collapsed = card.Height;

        card.IsExpanded = true;

        Assert.True(card.ShowChoices);
        Assert.True(card.Height > collapsed);
        Assert.Equal("收起 ▴", card.ExpandHint);
    }

    [Fact]
    public void ApplyChoices_KeepsAtMostFour()
    {
        var card = NewCard();

        card.ApplyChoices(Set(
            Choice(1, "一", 2), Choice(2, "二", 2), Choice(3, "三", 2), Choice(4, "四", 2), Choice(5, "五", 2)));

        Assert.Equal(4, card.ChoiceRows.Count);
    }

    [Fact]
    public void WaitingHint_IsHonestBeforeChoicesArrive()
    {
        var card = NewCard();

        card.MarkWaiting();

        Assert.True(card.IsWaiting);
        Assert.Equal("正在想怎么回…", card.ExpandHint);
    }

    [Fact]
    public void ChoiceRow_ShowsOutcomeAndTone()
    {
        var row = ChoiceRowViewModel.From(Choice(2, "确实贵，你是想囤还是自己用？", 2));

        Assert.Equal(2, row.Index);
        Assert.Equal("话术", row.ToneName);
        Assert.Contains("被安抚", row.OutcomeText);
        Assert.Contains("60%", row.OutcomeText);
        Assert.Equal("低风险", row.RiskText);
    }
}
