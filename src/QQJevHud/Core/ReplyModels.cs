namespace QQJevHud.Core;

/// <summary>A reply style ("话术"): a name plus the tone instructions fed to the generator.</summary>
public sealed record Tone(string Name, string Description);

/// <summary>One generated candidate reply, tagged with the tone it came from.</summary>
public sealed record ReplyCandidate(string Text, string ToneName);

/// <summary>
/// What Jev predicts will happen if this reply is sent — the "consequence" shown under each choice,
/// the way a visual novel previews where an option leads.
/// </summary>
public sealed record CandidateOutcome(
    string Reaction,
    int ReactionProbability,
    int RiskLevel,
    string RiskLabel);

/// <summary>
/// One selectable reply on the choice panel: the draft, its tone, and Jev's predicted outcome.
/// </summary>
public sealed record ChoiceItem(int Index, string Text, string ToneName, CandidateOutcome? Outcome)
{
    /// <summary>
    /// Higher = the better bet. A good reaction beats a merely likely one, then lower risk, then the
    /// reaction's own probability — so "they'd feel soothed, 55%" outranks "they'd feel brushed off, 70%".
    /// </summary>
    public int Rank => Outcome is null ? 0
        : Valence(Outcome.Reaction) * 1000 + (11 - Outcome.RiskLevel) * 40 + Outcome.ReactionProbability;

    private static int Valence(string reaction) => reaction switch
    {
        "被安抚、情绪缓和" => 2,
        "满意、顺利接住" => 2,
        "无明显变化" => 1,
        _ => 0            // 觉得被敷衍 / 可能更不满 / anything unknown
    };
}

/// <summary>
/// Everything the choice panel shows for one incoming message: who said what, Jev's judgment, and the
/// candidate replies with their predicted outcomes. Judgment and choices live in one surface so the
/// user reads the situation and picks a response in a single place.
/// </summary>
public sealed record ChoiceSet(
    string MessageId,
    string MessageText,
    string? Sender,
    DecisionCard Card,
    IReadOnlyList<ChoiceItem> Choices)
{
    /// <summary>The choices ordered safest-first, renumbered 1..n for display.</summary>
    public IReadOnlyList<ChoiceItem> Ordered =>
        Choices.OrderByDescending(choice => choice.Rank)
            .Select((choice, position) => choice with { Index = position + 1 })
            .ToArray();
}
