namespace QQJevHud.Core;

/// <summary>A reply style ("话术"): a name plus the tone instructions fed to the generator.</summary>
public sealed record Tone(string Name, string Description);

/// <summary>One generated candidate reply. Rank 1 = most suitable (after ranking).</summary>
public sealed record ReplyCandidate(string Text, string Style, string ToneName, int Rank = 0);

/// <summary>Candidates grouped under one tone (usually one 稳妥 + one 有个性).</summary>
public sealed record ReplyGroup(string ToneName, IReadOnlyList<ReplyCandidate> Candidates);

/// <summary>The reply draft for one incoming message: what to say, grouped by tone.</summary>
public sealed record ReplyDraft(
    string MessageId,
    string MessageText,
    IReadOnlyList<ReplyGroup> Groups)
{
    public IEnumerable<ReplyCandidate> All => Groups.SelectMany(group => group.Candidates);
}
