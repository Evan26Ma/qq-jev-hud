using QQJevHud.Core;

namespace QQJevHud.Tests;

public sealed class OverlayLayoutEngineTests
{
    [Fact]
    public void Arrange_UsesOnlySafePlacements_WhenMessagesAreDense()
    {
        var card = new DecisionCard("a", "主题", "问题", Array.Empty<DecisionOption>(), 4, "需要留意", "认真回应", 80, AnalysisState.Ready);
        var messages = new[]
        {
            (new ChatMessage("a", "s", MessageDirection.Incoming, "第一条", .9, new ScreenRect(30, 30, 180, 30)), card),
            (new ChatMessage("b", "s", MessageDirection.Incoming, "第二条", .9, new ScreenRect(30, 74, 180, 30)), card with { MessageId = "b" }),
            (new ChatMessage("c", "s", MessageDirection.Incoming, "第三条", .9, new ScreenRect(30, 118, 180, 30)), card with { MessageId = "c" })
        };

        var layouts = new OverlayLayoutEngine().Arrange(messages, new ScreenRect(0, 0, 340, 280));

        Assert.Equal(3, layouts.Count);
        Assert.Contains(layouts, item => item.Placement is CardPlacementKind.SideRail or CardPlacementKind.Marker);
        Assert.All(layouts, item => Assert.True(item.Bounds.Left >= 0 && item.Bounds.Top >= 0));
    }
}
