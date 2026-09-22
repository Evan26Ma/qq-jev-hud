using System.IO;
using QQJevHud.Services;

namespace QQJevHud.Tests;

public sealed class ContactNoteStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"qqjevhud-notes-{Guid.NewGuid():N}");

    public ContactNoteStoreTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Load_ReturnsNullWhenNoteMissing()
    {
        Assert.Null(new ContactNoteStore(_directory).Load("没有这个人"));
    }

    [Fact]
    public void Load_ReadsTheContactNote()
    {
        File.WriteAllText(Path.Combine(_directory, "茶.md"), "雷区：别提加班。近况：在准备考试。");

        var note = new ContactNoteStore(_directory).Load("茶");

        Assert.NotNull(note);
        Assert.Contains("雷区", note!);
    }

    [Fact]
    public void SanitizeFileName_StripsPathTraversalAndInvalidCharacters()
    {
        var safe = ContactNoteStore.SanitizeFileName("../../etc/passwd");

        Assert.DoesNotContain("..", safe);
        Assert.DoesNotContain("/", safe);
        Assert.DoesNotContain("\\", safe);
        Assert.Null(new ContactNoteStore(_directory).Load("../secret"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch { /* best-effort cleanup */ }
    }
}
