namespace QQJevHud.Core;

public sealed class ChatDetector : IChatDetector
{
    private const double GroupTolerance = 30;

    public IReadOnlyList<ChatMessage> Detect(
        IReadOnlyList<OcrLine> lines,
        string sessionId,
        ScreenRect historyBounds)
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

        var messages = new List<ChatMessage>();
        foreach (var group in groups)
        {
            var ordered = group.OrderBy(item => item.Bounds.Left).ToList();
            var text = string.Concat(ordered.Select(item => item.Text));
            if (string.IsNullOrWhiteSpace(text)) continue;

            var left = ordered.Min(item => item.Bounds.Left);
            var top = ordered.Min(item => item.Bounds.Top);
            var right = ordered.Max(item => item.Bounds.Right);
            var bottom = ordered.Max(item => item.Bounds.Bottom);
            var bounds = new ScreenRect(left, top, right - left, bottom - top);
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
                bounds));
        }

        return messages;
    }
}
