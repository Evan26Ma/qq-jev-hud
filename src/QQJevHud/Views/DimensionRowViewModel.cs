using System.Windows.Media;
using QQJevHud.Core;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace QQJevHud.Views;

/// <summary>Display projection of one <see cref="JudgmentDimension"/> for the overlay card.</summary>
public sealed class DimensionRowViewModel
{
    // Fallbacks keep the card rendering even if a theme resource is missing.
    private static readonly Brush FillFallback = Freeze(Color.FromRgb(0x3B, 0x82, 0xF6));
    private static readonly Brush MutedFallback = Freeze(Color.FromRgb(0x8B, 0x90, 0x98));

    public required string Label { get; init; }
    public required IReadOnlyList<OptionRowViewModel> Options { get; init; }
    public string Star => Highlight ? "★ " : string.Empty;
    public bool Highlight { get; init; }

    public static DimensionRowViewModel From(JudgmentDimension dimension) => new()
    {
        Label = dimension.Label,
        Highlight = dimension.Highlight,
        Options = dimension.Options.Select((option, index) => new OptionRowViewModel
        {
            Label = option.Label,
            Probability = option.Probability,
            FillBrush = index == 0
                ? Lookup("BarFillBrush", FillFallback)
                : Lookup("BarFillMutedBrush", MutedFallback)
        }).ToArray()
    };

    private static Brush Lookup(string key, Brush fallback) =>
        Application.Current?.TryFindResource(key) as Brush ?? fallback;

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}

/// <summary>One option row: label, percentage and the bar fill colour.</summary>
public sealed class OptionRowViewModel
{
    public required string Label { get; init; }
    public required int Probability { get; init; }
    public required Brush FillBrush { get; init; }
}
