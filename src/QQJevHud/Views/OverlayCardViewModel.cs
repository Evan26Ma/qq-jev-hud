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
