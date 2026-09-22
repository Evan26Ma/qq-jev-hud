namespace QQJevHud.Core;

/// <summary>
/// Single source of truth for card geometry: the layout engine and the overlay both size cards
/// from here, so a rich card (with dimensions) grows taller without the two drifting apart.
/// </summary>
public static class CardMetrics
{
    public const double Width = 280;
    public const double BaseHeight = 156;          // plain card (options + footer)
    public const double DimensionRowHeight = 46;   // one dimension: label + two option rows
    public const double QuoteHeight = 32;          // the quoted message so the card can be matched
    public const int MaxDimensions = 4;            // keep the card readable and on-screen

    public static double HeightFor(DecisionCard card) =>
        (card.Dimensions.Count == 0
            ? BaseHeight
            : 150 + Math.Min(card.Dimensions.Count, MaxDimensions) * DimensionRowHeight) + QuoteHeight;
}
