using QQJevHud.Core;

namespace QQJevHud.Tests;

/// <summary>
/// The card must show who said what: in a group the speaker's name rides above the bubble and has
/// to be peeled off the message body, so the card can be matched to the chat after a scroll.
/// </summary>
public sealed class SenderDetectionTests
{
    private static readonly ScreenRect History = new(100, 100, 900, 500);

    [Fact]
    public void GroupChat_StripsSpeakerNameAndTagsFromMessageBody()
    {
        var detector = new ChatDetector();
        var messages = detector.Detect(new[]
        {
            new OcrLine("小轩学长（27课程已上线）", 0.95, new ScreenRect(170, 165, 150, 16)),
            new OcrLine("[管理员]", 0.94, new ScreenRect(252, 163, 60, 14)),
            new OcrLine("讲义实拍，十分精美哦~", 0.93, new ScreenRect(170, 186, 180, 22))
        }, "s-1", History, isGroupChat: true);

        Assert.Single(messages);
        Assert.Equal("小轩学长", messages[0].Sender);
        Assert.Equal("讲义实拍，十分精美哦~", messages[0].Text);
    }

    [Fact]
    public void GroupChat_KeepsProseLineAsContent()
    {
        var detector = new ChatDetector();
        // No separate name line: the whole thing is one bubble from the speaker.
        var messages = detector.Detect(new[]
        {
            new OcrLine("我昨天买的M7五级弹", 0.93, new ScreenRect(170, 170, 160, 22))
        }, "s-1", History, isGroupChat: true);

        Assert.Single(messages);
        Assert.Null(messages[0].Sender);
        Assert.Equal("我昨天买的M7五级弹", messages[0].Text);
    }

    [Fact]
    public void DirectChat_DoesNotTreatTextAsSpeakerName()
    {
        var detector = new ChatDetector();
        var messages = detector.Detect(new[]
        {
            new OcrLine("这个两台设备", 0.93, new ScreenRect(170, 168, 110, 22)),
            new OcrLine("是只要不在线同时使用就可以了嘛", 0.93, new ScreenRect(170, 192, 210, 22))
        }, "s-1", History, isGroupChat: false);

        Assert.Single(messages);
        Assert.Null(messages[0].Sender);
        Assert.Equal("这个两台设备是只要不在线同时使用就可以了嘛", messages[0].Text);
    }

    [Fact]
    public void GroupChat_DoesNotMistakeASentenceForASpeaker()
    {
        var detector = new ChatDetector();
        var messages = detector.Detect(new[]
        {
            new OcrLine("好哒，嘿嘿", 0.93, new ScreenRect(170, 168, 90, 20)),
            new OcrLine("那就这么定了", 0.93, new ScreenRect(170, 192, 110, 22))
        }, "s-1", History, isGroupChat: true);

        Assert.Single(messages);
        Assert.Null(messages[0].Sender);
        Assert.Equal("好哒，嘿嘿那就这么定了", messages[0].Text);
    }
}
