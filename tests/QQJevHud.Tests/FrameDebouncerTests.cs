using QQJevHud.Services;

namespace QQJevHud.Tests;

public sealed class FrameDebouncerTests
{
    [Fact]
    public void Observe_DeliversExactlyOnceAfterFrameSettles()
    {
        var gate = new FrameDebouncer(TimeSpan.FromMilliseconds(900));
        var time = DateTimeOffset.UtcNow;
        Assert.False(gate.Observe("a", time));
        Assert.False(gate.Observe("a", time.AddMilliseconds(899)));
        Assert.True(gate.Observe("a", time.AddMilliseconds(900)));
        Assert.False(gate.Observe("a", time.AddMilliseconds(1200)));
        Assert.False(gate.Observe("b", time.AddMilliseconds(1300)));
    }
}
