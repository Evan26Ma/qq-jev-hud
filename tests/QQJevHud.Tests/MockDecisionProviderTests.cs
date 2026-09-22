using QQJevHud.Core;

namespace QQJevHud.Tests;

public sealed class MockDecisionProviderTests
{
    [Fact]
    public async Task Analyze_ReturnsStableWholePercentages()
    {
        var message = new ChatMessage("fixed", "s", MessageDirection.Incoming, "你是不是忘了？", .9, new ScreenRect(0, 0, 10, 10));
        var provider = new MockDecisionProvider();
        var first = await provider.AnalyzeAsync(message, Array.Empty<ChatMessage>(), CancellationToken.None);
        var second = await provider.AnalyzeAsync(message, Array.Empty<ChatMessage>(), CancellationToken.None);
        Assert.Equal(first.MessageId, second.MessageId);
        Assert.Equal(first.Topic, second.Topic);
        Assert.Equal(first.Options.Select(option => (option.Label, option.Probability)), second.Options.Select(option => (option.Label, option.Probability)));
        Assert.Equal(100, first.Options.Sum(option => option.Probability));
        Assert.InRange(first.RiskLevel, 1, 10);
    }
}
