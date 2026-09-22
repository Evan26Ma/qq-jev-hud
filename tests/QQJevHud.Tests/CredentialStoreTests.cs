using QQJevHud.Services;

namespace QQJevHud.Tests;

public sealed class CredentialStoreTests
{
    [Fact]
    public void ReadTypeSafeKey_ReturnsCleanKey_WithoutControlCharacters()
    {
        var key = CredentialStore.ReadTypeSafeKey();

        Assert.False(string.IsNullOrWhiteSpace(key), "expected the stored API key to be readable");
        Assert.DoesNotContain(key!, ch => char.IsControl(ch));
        Assert.InRange(key!.Length, 20, 400);
    }
}
