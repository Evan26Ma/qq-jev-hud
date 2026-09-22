using System.Text;

namespace QQJevHud.Services;

/// <summary>
/// Contact background cards: a plain <c>notes/&lt;联系人&gt;.md</c> file next to the project holds the
/// agreements, sore spots and recent context for one contact. Saving the file makes it take effect
/// without a restart; nothing here is ever uploaded except as part of a judgment request.
/// </summary>
public sealed class ContactNoteStore
{
    private const int MaxCharacters = 1200;

    private readonly string? _directory;
    private string? _cachedName;
    private string? _cachedText;
    private DateTime _cachedWriteTime;

    public ContactNoteStore(string? directory = null)
    {
        _directory = directory ?? ResolveDirectory();
    }

    /// <summary>The notes folder, or null when it cannot be located.</summary>
    public string? NotesDirectory => _directory;

    /// <summary>Reads the note for a contact, or null when there is none. Never throws.</summary>
    public string? Load(string? contactName)
    {
        if (string.IsNullOrWhiteSpace(contactName) || _directory is null) return null;
        try
        {
            var name = SanitizeFileName(contactName);
            if (name.Length == 0) return null;
            if (string.Equals(_cachedName, name, StringComparison.OrdinalIgnoreCase))
            {
                var path = Path.Combine(_directory, name + ".md");
                var writeTime = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
                if (writeTime == _cachedWriteTime) return _cachedText;   // hot-reload without a restart
            }
            var file = Path.Combine(_directory, name + ".md");
            if (!File.Exists(file))
            {
                _cachedName = name;
                _cachedWriteTime = DateTime.MinValue;
                _cachedText = null;
                return null;
            }
            var text = File.ReadAllText(file, Encoding.UTF8).Trim();
            _cachedName = name;
            _cachedText = text;
            _cachedWriteTime = File.GetLastWriteTimeUtc(file);
            return text.Length <= MaxCharacters ? text : text[..MaxCharacters];
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Strips characters that cannot appear in a file name (also blocks path traversal).</summary>
    public static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (var ch in name.Trim())
        {
            if (!invalid.Contains(ch) && ch != '.' && ch != '/' && ch != '\\') builder.Append(ch);
        }
        return builder.ToString().Trim();
    }

    private static string? ResolveDirectory()
    {
        foreach (var root in CandidateRoots())
        {
            var candidate = Path.Combine(root, "notes");
            if (Directory.Exists(candidate)) return candidate;
            // Fall back to the repository root so the folder can be created on first use.
            if (File.Exists(Path.Combine(root, ".gitignore")) && File.Exists(Path.Combine(root, "QQJevHud.sln")))
                return candidate;
        }
        return null;
    }

    private static IEnumerable<string> CandidateRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (seen.Add(directory.FullName)) yield return directory.FullName;
            }
        }
    }
}
