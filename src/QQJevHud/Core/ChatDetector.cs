using System.Text.RegularExpressions;

namespace QQJevHud.Core;

public sealed partial class ChatDetector : IChatDetector
{
    private const double GroupTolerance = 30;
    private const double SpeakerRowTolerance = 8;    // vertical jitter within one rendered row
    private const double SpeakerGapTolerance = 64;   // a stranded name row this close above a bubble

    /// <summary>Badges that ride along with a QQ display name, e.g. （27课程已上线） / [管理员].</summary>
    [GeneratedRegex(@"[（(\[][^）)\]]*[）)\]]")]
    private static partial Regex Badge();

    [GeneratedRegex(@"\b(?:[Ll][Vv])\s*\d+\b")]
    private static partial Regex Level();

    /// <summary>Sentence punctuation: its presence means the line is prose, not a display name.</summary>
    [GeneratedRegex(@"[。！？，、；：…!?,;]")]
    private static partial Regex ProsePunctuation();

    public IReadOnlyList<ChatMessage> Detect(
        IReadOnlyList<OcrLine> lines,
        string sessionId,
        ScreenRect historyBounds,
        bool isGroupChat = false)
    {
        var usable = lines
            .Select(line => line with { Text = TextNormalizer.Normalize(line.Text) })
            .Where(line => line.Confidence >= 0.35 && !string.IsNullOrWhiteSpace(line.Text))
            .Where(line => !TextNormalizer.IsMetadata(line.Text))
            .Where(line => line.Bounds.Width > 4 && line.Bounds.Height > 4)
            .OrderBy(line => line.Bounds.Top)
            .ThenBy(line => line.Bounds.Left)
            .ToList();

        var groups = new List<List<OcrLine>>();
        foreach (var line in usable)
        {
            var group = groups.LastOrDefault();
            if (group is null || line.Bounds.Top - group.Max(item => item.Bounds.Bottom) > GroupTolerance)
            {
                group = new List<OcrLine>();
                groups.Add(group);
            }
            group.Add(line);
        }

        // In a group chat the speaker's name sits above the bubble. Sometimes it shares the bubble's
        // OCR group; sometimes it is stranded in its own group just above. Resolve both shapes first.
        var labels = isGroupChat ? ResolveSpeakerLabels(groups) : new Dictionary<int, SpeakerLabel>();

        var messages = new List<ChatMessage>();
        for (var index = 0; index < groups.Count; index++)
        {
            var ordered = groups[index].OrderBy(item => item.Bounds.Left).ToList();
            labels.TryGetValue(index, out var label);
            if (label is { OwnGroup: false })
            {
                var row = new HashSet<OcrLine>(label.RowLines, ReferenceEqualityComparer.Instance);
                ordered = ordered.Where(item => !row.Contains(item) && !IsBadgeOnly(item.Text)).ToList();
            }
            else
            {
                ordered = ordered.Where(item => !IsBadgeOnly(item.Text)).ToList();
            }

            var text = string.Concat(ordered.Select(item => item.Text));
            if (string.IsNullOrWhiteSpace(text)) continue;

            var left = ordered.Min(item => item.Bounds.Left);
            var topMost = ordered.Min(item => item.Bounds.Top);
            var right = ordered.Max(item => item.Bounds.Right);
            var bottom = ordered.Max(item => item.Bounds.Bottom);
            var bounds = new ScreenRect(left, topMost, right - left, bottom - topMost);
            var direction = bounds.CenterX < historyBounds.X + historyBounds.Width * 0.56
                ? MessageDirection.Incoming
                : MessageDirection.Outgoing;
            var normalized = TextNormalizer.Normalize(text);
            messages.Add(new ChatMessage(
                TextNormalizer.Fingerprint(sessionId, direction, normalized),
                sessionId,
                direction,
                normalized,
                ordered.Average(item => item.Confidence),
                bounds,
                label?.Name));
        }

        return messages;
    }

    /// <summary>Speaker name found for one OCR group, and whether it came from that same group.</summary>
    private sealed record SpeakerLabel(string? Name, bool OwnGroup, IReadOnlyList<OcrLine> RowLines);

    /// <summary>
    /// Maps group index → speaker. Two shapes: the name row is the top row of the bubble's own group
    /// (strip it from the body), or the name sits alone in the group right above (keep the bubble whole).
    /// </summary>
    private static Dictionary<int, SpeakerLabel> ResolveSpeakerLabels(List<List<OcrLine>> groups)
    {
        var labels = new Dictionary<int, SpeakerLabel>();
        var stranded = new HashSet<int>();

        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            if (stranded.Contains(index)) continue;

            // Shape 1: a name row inside this group, above the bubble content.
            if (group.Count > 1 && NameRow(group) is { } row)
            {
                labels[index] = new SpeakerLabel(row.Name, OwnGroup: false, row.Lines);
                continue;
            }

            // Shape 2: this whole group is just a name, and a bubble follows right beneath it.
            if (group.Count <= 2 && IsNameOnly(group) && index + 1 < groups.Count && !labels.ContainsKey(index + 1) &&
                groups[index + 1].Min(item => item.Bounds.Top) - group.Max(item => item.Bounds.Bottom) <= SpeakerGapTolerance)
            {
                var name = ReadName(group.OrderBy(item => item.Bounds.Left).First().Text);
                labels[index + 1] = new SpeakerLabel(name, OwnGroup: true, Array.Empty<OcrLine>());
                stranded.Add(index);
            }
        }

        return labels;
    }

    /// <summary>
    /// The topmost rendered row if it looks like a display name rather than message content. The row
    /// may hold several fragments (the name plus tags to its right, or a tag above it).
    /// </summary>
    private static (string Name, IReadOnlyList<OcrLine> Lines)? NameRow(List<OcrLine> group)
    {
        var ordered = group.OrderBy(item => item.Bounds.Top).ThenBy(item => item.Bounds.Left).ToList();
        var bottomMost = ordered.Max(item => item.Bounds.Bottom);

        foreach (var candidate in ordered)
        {
            // Must have content below it, otherwise the "name" is simply the whole message.
            if (bottomMost - candidate.Bounds.Bottom < SpeakerRowTolerance) continue;
            var name = ReadName(candidate.Text);
            if (name is null) continue;
            var rowTop = candidate.Bounds.Top;
            return (name, ordered.Where(item => item.Bounds.Top - rowTop <= SpeakerRowTolerance).ToList());
        }

        return null;
    }

    /// <summary>The group is nothing but a display name (all lines are name-like or pure tags).</summary>
    private static bool IsNameOnly(List<OcrLine> group) =>
        group.Count > 0 && group.All(item => ReadName(item.Text) is not null || IsBadgeOnly(item.Text));

    /// <summary>A short, prose-free line with badges stripped — a display name, or null.</summary>
    private static string? ReadName(string raw)
    {
        var text = Badge().Replace(Level().Replace(TextNormalizer.Normalize(raw), " "), " ").Trim();
        if (text.Length is 0 or > 12) return null;
        if (ProsePunctuation().IsMatch(text)) return null;
        return text;
    }

    /// <summary>A line that is nothing but a tag/level, e.g. "[管理员]" or "LV41".</summary>
    private static bool IsBadgeOnly(string raw)
    {
        var text = TextNormalizer.Normalize(raw);
        return text.Length > 0 && Badge().Replace(Level().Replace(text, " "), " ").Trim().Length == 0;
    }
}
