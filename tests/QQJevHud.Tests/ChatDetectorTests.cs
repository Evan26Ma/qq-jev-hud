using QQJevHud.Core;

namespace QQJevHud.Tests;

public sealed class ChatDetectorTests
{
    [Fact]
    public void Detect_ClassifiesLeftAndRightMessages_AndDropsTimeLabels()
    {
        var detector = new ChatDetector();
        var history = new ScreenRect(100, 100, 900, 500);
        var messages = detector.Detect(new[]
        {
            new OcrLine("21:37", 0.99, new ScreenRect(520, 120, 40, 18)),
            new OcrLine("你今天是不是", 0.93, new ScreenRect(170, 170, 110, 22)),
            new OcrLine("又忘了", 0.93, new ScreenRect(170, 198, 70, 22)),
            new OcrLine("记得", 0.96, new ScreenRect(790, 270, 48, 22))
        }, "s-1", history);

        Assert.Equal(2, messages.Count);
        Assert.Equal(MessageDirection.Incoming, messages[0].Direction);
        Assert.Equal("你今天是不是又忘了", messages[0].Text);
        Assert.Equal(MessageDirection.Outgoing, messages[1].Direction);
    }
}
