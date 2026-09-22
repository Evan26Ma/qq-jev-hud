using QQJevHud.Core;

namespace QQJevHud.Tests;

public sealed class TextNormalizerTests
{
    [Fact]
    public void Normalize_CollapsesWhitespace_AndFingerprintIsStable()
    {
        Assert.Equal("你 好", TextNormalizer.Normalize("  你\n 好  "));
        var first = TextNormalizer.Fingerprint("session", MessageDirection.Incoming, "你好");
        var second = TextNormalizer.Fingerprint("session", MessageDirection.Incoming, "你好");
        var different = TextNormalizer.Fingerprint("session", MessageDirection.Outgoing, "你好");
        Assert.Equal(first, second);
        Assert.NotEqual(first, different);
    }

    [Theory]
    [InlineData("21:37")]
    [InlineData("今天")]
    public void Metadata_IsFiltered(string input) => Assert.True(TextNormalizer.IsMetadata(input));
}
