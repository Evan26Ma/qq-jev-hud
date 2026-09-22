using System.Windows.Media;
using QQJevHud.Core;

namespace QQJevHud.Views;

public sealed class OverlayCardViewModel
{
    public required DecisionCard Card { get; init; }
    public double Left { get; init; }
    public double Top { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public bool IsMarker { get; init; }
    public bool ShowOptions { get; init; } = true;
    public bool ShowRisk { get; init; } = true;
    public bool ShowAdvice { get; init; } = true;

    /// <summary>The message this card judges, so it can be matched to the chat after a scroll.</summary>
    public string MessageText { get; init; } = string.Empty;

    /// <summary>Speaker in a group chat, or the contact in a 1:1 chat.</summary>
    public string? Sender { get; init; }

    public string SenderPrefix => string.IsNullOrWhiteSpace(Sender) ? string.Empty : $"{Sender}：";
    public string QuoteText => Truncate(MessageText, 52);
    public bool HasQuote => !string.IsNullOrWhiteSpace(MessageText);

    private static string Truncate(string text, int max) =>
        string.IsNullOrEmpty(text) || text.Length <= max ? text : text[..max] + "…";

    /// <summary>Rich dimensions, trimmed to two options each so the card stays compact.</summary>
    public IReadOnlyList<DimensionRowViewModel> Dimensions => Card.Dimensions.Count == 0
        ? Array.Empty<DimensionRowViewModel>()
        : Card.VisibleDimensions(CardMetrics.MaxDimensions)
            .Select(dimension => DimensionRowViewModel.From(dimension with { Options = dimension.Options.Take(2).ToArray() }))
            .ToArray();

    public bool HasDimensions => Card.Dimensions.Count > 0;
    public bool ShowDimensions => ShowOptions && HasDimensions;
    public bool ShowPlainOptions => ShowOptions && !HasDimensions;

    public string RiskText => Card.RiskLevel == 0 ? Card.RiskLabel : $"{Card.RiskLabel} · {Card.RiskLevel} / 10";
    public System.Windows.Media.Brush RiskBrush => Card.RiskLevel >= 8
        ? System.Windows.Media.Brushes.IndianRed
        : Card.RiskLevel >= 5 ? System.Windows.Media.Brushes.DarkGoldenrod : System.Windows.Media.Brushes.SeaGreen;
    public string StateText => Card.State switch
    {
        AnalysisState.Loading => "Jev 正在判断…",
        AnalysisState.LowConfidence => "上下文不足",
        AnalysisState.Failed => "本次判断失败",
        AnalysisState.Unavailable => "服务未启用",
        _ => $"置信度 {Card.Confidence}%"
    };
}
