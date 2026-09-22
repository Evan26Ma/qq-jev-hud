using QQJevHud.Core;

namespace QQJevHud.Tests;

/// <summary>
/// Group title badges and level titles are decoration. They must never reach the model as if they were
/// something a person said, and a name line must still be recognised.
/// </summary>
public sealed class GroupDecorationTests
{
    private static readonly ScreenRect History = new(100, 100, 900, 500);

    [Fact]
    public void LevelBadge_IsDroppedFromTheConversation()
    {
        var detector = new ChatDetector();
        var messages = detector.Detect(new[]
        {
            new OcrLine("LV41", 0.95, new ScreenRect(520, 168, 40, 16)),
            new OcrLine("这题我不会，有人会吗", 0.93, new ScreenRect(170, 190, 200, 22))
        }, "s-1", History, isGroupChat: true);

        Assert.Single(messages);
        Assert.Equal("这题我不会，有人会吗", messages[0].Text);
        Assert.DoesNotContain("LV41", messages[0].Text);
    }

    [Fact]
    public void LevelTitleWithTierWord_IsDropped()
    {
        var detector = new ChatDetector();
        var messages = detector.Detect(new[]
        {
            new OcrLine("Lv10 青铜", 0.94, new ScreenRect(520, 168, 70, 16)),
            new OcrLine("我也在刷数学", 0.93, new ScreenRect(170, 190, 140, 22))
        }, "s-1", History, isGroupChat: true);

        Assert.Single(messages);
        Assert.Equal("我也在刷数学", messages[0].Text);
    }

    [Fact]
    public void SpeakerNameIsKept_ButItsLevelBadgeIsNot()
    {
        var detector = new ChatDetector();
        var messages = detector.Detect(new[]
        {
            new OcrLine("小轩学长", 0.95, new ScreenRect(170, 165, 90, 16)),
            new OcrLine("LV41 黄金", 0.94, new ScreenRect(262, 165, 70, 16)),
            new OcrLine("讲义实拍，十分精美", 0.93, new ScreenRect(170, 190, 170, 22))
        }, "s-1", History, isGroupChat: true);

        Assert.Single(messages);
        Assert.Equal("小轩学长", messages[0].Sender);
        Assert.Equal("讲义实拍，十分精美", messages[0].Text);
    }

    [Fact]
    public void BareTierWord_IsNotTreatedAsASpeaker()
    {
        var detector = new ChatDetector();
        var messages = detector.Detect(new[]
        {
            new OcrLine("黄金", 0.94, new ScreenRect(170, 165, 40, 16)),
            new OcrLine("明天继续吧", 0.93, new ScreenRect(170, 190, 100, 22))
        }, "s-1", History, isGroupChat: true);

        Assert.Single(messages);
        Assert.Null(messages[0].Sender);
        Assert.Equal("明天继续吧", messages[0].Text);
    }
}
