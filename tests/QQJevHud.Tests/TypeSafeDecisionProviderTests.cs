using QQJevHud.Core;

namespace QQJevHud.Tests;

public sealed class TypeSafeDecisionProviderTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnalyzeAsync_WithTestMessage_ReturnsRealDecision_NotFailed()
    {
        var provider = new TypeSafeDecisionProvider();
        var message = new ChatMessage("t-1", "s-1", MessageDirection.Incoming,
            "I have been trying to fix this for three days and I am really frustrated.", 0.9,
            new ScreenRect(0, 0, 10, 10));

        var card = await provider.AnalyzeAsync(message, Array.Empty<ChatMessage>(), CancellationToken.None);

        Assert.NotEqual(AnalysisState.Failed, card.State);
        Assert.NotEqual(AnalysisState.Unavailable, card.State);
        Assert.NotEmpty(card.Options);
    }
}
