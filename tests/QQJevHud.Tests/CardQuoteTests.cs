using QQJevHud.Core;
using QQJevHud.Views;

namespace QQJevHud.Tests;

/// <summary>The card quotes the judged message so it stays identifiable while the chat scrolls.</summary>
public sealed class CardQuoteTests
{
    [Fact]
    public void Arrange_AttachesMessageTextAndSenderToEveryCard()
    {
        var card = new DecisionCard("a", "主题", "问题", Array.Empty<DecisionOption>(), 3, "低风险", "建议", 80, AnalysisState.Ready);
        var message = new ChatMessage("a", "s", MessageDirection.Incoming, "我昨天买的M7五级弹", .9,
            new ScreenRect(30, 30, 180, 30), "零");

        var layouts = new OverlayLayoutEngine().Arrange(new[] { (message, card) }, new ScreenRect(0, 0, 600, 500));

        Assert.Single(layouts);
        Assert.Equal("我昨天买的M7五级弹", layouts[0].MessageText);
        Assert.Equal("零", layouts[0].Sender);
    }

    [Fact]
    public void ViewModel_ShowsSpeakerPrefixAndTruncatesLongMessages()
    {
        var card = new DecisionCard("a", "主题", "问题", Array.Empty<DecisionOption>(), 3, "低风险", "建议", 80, AnalysisState.Ready);
        var long_text = new string('测', 120);
        var viewModel = new OverlayCardViewModel { Card = card, MessageText = long_text, Sender = "零" };

        Assert.True(viewModel.HasQuote);
        Assert.Equal("零：", viewModel.SenderPrefix);
        Assert.True(viewModel.QuoteText.Length < long_text.Length);
        Assert.EndsWith("…", viewModel.QuoteText);
    }

    [Fact]
    public void ViewModel_OmitsSpeakerPrefixWhenUnknown()
    {
        var card = new DecisionCard("a", "主题", "问题", Array.Empty<DecisionOption>(), 3, "低风险", "建议", 80, AnalysisState.Ready);
        var viewModel = new OverlayCardViewModel { Card = card, MessageText = "你好" };

        Assert.Equal(string.Empty, viewModel.SenderPrefix);
        Assert.True(viewModel.HasQuote);
        Assert.False(new OverlayCardViewModel { Card = card }.HasQuote);
    }

    [Fact]
    public void CardMetrics_ReserveRoomForTheQuote()
    {
        var card = new DecisionCard("a", "主题", "问题", Array.Empty<DecisionOption>(), 3, "低风险", "建议", 80, AnalysisState.Ready);

        Assert.True(CardMetrics.HeightFor(card) >= CardMetrics.BaseHeight + CardMetrics.QuoteHeight);
    }

    [Fact]
    public void Arrange_TightSpace_OutlinesTheMessageInsteadOfUsingATinyDot()
    {
        var card = new DecisionCard("a", "主题", "问题", Array.Empty<DecisionOption>(), 3, "低风险", "建议", 80, AnalysisState.Ready);
        var message = new ChatMessage("a", "s", MessageDirection.Incoming, "我是做题", .9,
            new ScreenRect(40, 40, 160, 30), "零");

        // A chat area barely larger than the message: no room for a card anywhere.
        var layouts = new OverlayLayoutEngine().Arrange(new[] { (message, card) }, new ScreenRect(0, 0, 240, 120));

        var marker = Assert.Single(layouts, layout => layout.Placement == CardPlacementKind.Marker);
        // The outline wraps the message (plus padding) rather than being an 18x18 dot.
        Assert.True(marker.Bounds.Width > 100, $"expected an outline, got width {marker.Bounds.Width}");
        Assert.True(marker.Bounds.Height > 20, $"expected an outline, got height {marker.Bounds.Height}");
        Assert.True(marker.Bounds.Left <= message.Bounds.Left);
        Assert.True(marker.Bounds.Top <= message.Bounds.Top);
        Assert.True(marker.Bounds.Right >= message.Bounds.Right);
        Assert.True(marker.Bounds.Bottom >= message.Bounds.Bottom);
    }
}
