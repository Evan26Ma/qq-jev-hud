namespace QQJevHud.Core;

public sealed class OverlayLayoutEngine
{
    private const double CardWidth = 280;
    private const double CardHeight = 156;
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
            var below = new ScreenRect(item.Message.Bounds.Left, item.Message.Bounds.Bottom + Gap, CardWidth, CardHeight);
            if (Fits(below, chatBounds, blocked))
            {
                result.Add(new CardLayout(item.Message.Id, below, CardPlacementKind.BelowMessage, item.Card));
                blocked.Add(below);
                continue;
            }

            var beside = new ScreenRect(item.Message.Bounds.Right + Gap, item.Message.Bounds.Top, CardWidth, CardHeight);
            if (Fits(beside, chatBounds, blocked))
            {
                result.Add(new CardLayout(item.Message.Id, beside, CardPlacementKind.BesideMessage, item.Card));
                blocked.Add(beside);
                continue;
            }

            var rail = new ScreenRect(chatBounds.Right - CardWidth - Gap, railY, CardWidth, CardHeight);
            if (rail.Bottom <= chatBounds.Bottom - Gap && !blocked.Any(rect => rect.Intersects(rail)))
            {
                result.Add(new CardLayout(item.Message.Id, rail, CardPlacementKind.SideRail, item.Card));
                blocked.Add(rail);
                railY = rail.Bottom + Gap;
                continue;
            }

            var marker = new ScreenRect(Math.Min(item.Message.Bounds.Right + Gap, chatBounds.Right - 22), item.Message.Bounds.Top, 18, 18);
            result.Add(new CardLayout(item.Message.Id, marker, CardPlacementKind.Marker, item.Card));
        }

        return result;
    }

    private static bool Fits(ScreenRect candidate, ScreenRect container, IEnumerable<ScreenRect> blocked) =>
        candidate.Left >= container.Left && candidate.Top >= container.Top && candidate.Right <= container.Right &&
        candidate.Bottom <= container.Bottom && !blocked.Any(rect => rect.Intersects(candidate));
}
