namespace QQJevHud.Core;

public sealed class OverlayLayoutEngine
{
    private const double CardWidth = CardMetrics.Width;
    private const double Gap = 8;

    public IReadOnlyList<CardLayout> Arrange(
        IReadOnlyList<(ChatMessage Message, DecisionCard Card)> cards,
        ScreenRect chatBounds)
    {
        var result = new List<CardLayout>();
        var blocked = cards.Select(item => item.Message.Bounds.Inflate(10, 8)).ToList();
        var railY = chatBounds.Top + Gap;

        foreach (var item in cards.OrderByDescending(pair => pair.Message.Bounds.Top))
        {
            var cardHeight = CardMetrics.HeightFor(item.Card);
            var below = new ScreenRect(item.Message.Bounds.Left, item.Message.Bounds.Bottom + Gap, CardWidth, cardHeight);
            if (Fits(below, chatBounds, blocked))
            {
                result.Add(Layout(item, below, CardPlacementKind.BelowMessage));
                blocked.Add(below);
                continue;
            }

            var beside = new ScreenRect(item.Message.Bounds.Right + Gap, item.Message.Bounds.Top, CardWidth, cardHeight);
            if (Fits(beside, chatBounds, blocked))
            {
                result.Add(Layout(item, beside, CardPlacementKind.BesideMessage));
                blocked.Add(beside);
                continue;
            }

            var rail = new ScreenRect(chatBounds.Right - CardWidth - Gap, railY, CardWidth, cardHeight);
            if (rail.Bottom <= chatBounds.Bottom - Gap && !blocked.Any(rect => rect.Intersects(rail)))
            {
                result.Add(Layout(item, rail, CardPlacementKind.SideRail));
                blocked.Add(rail);
                railY = rail.Bottom + Gap;
                continue;
            }

            var marker = new ScreenRect(Math.Min(item.Message.Bounds.Right + Gap, chatBounds.Right - 22), item.Message.Bounds.Top, 18, 18);
            result.Add(Layout(item, marker, CardPlacementKind.Marker));
        }

        return result;
    }

    /// <summary>Every card carries the message it judges, so the user can match it to the chat.</summary>
    private static CardLayout Layout((ChatMessage Message, DecisionCard Card) item, ScreenRect bounds, CardPlacementKind placement) =>
        new(item.Message.Id, bounds, placement, item.Card)
        {
            MessageText = item.Message.Text,
            Sender = item.Message.Sender
        };

    private static bool Fits(ScreenRect candidate, ScreenRect container, IEnumerable<ScreenRect> blocked) =>
        candidate.Left >= container.Left && candidate.Top >= container.Top && candidate.Right <= container.Right &&
        candidate.Bottom <= container.Bottom && !blocked.Any(rect => rect.Intersects(candidate));
}
