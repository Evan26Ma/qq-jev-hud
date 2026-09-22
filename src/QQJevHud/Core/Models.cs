namespace QQJevHud.Core;

public readonly record struct ScreenRect(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Intersects(ScreenRect other) =>
        Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;

    public ScreenRect Inflate(double horizontal, double vertical) =>
        new(X - horizontal, Y - vertical, Width + horizontal * 2, Height + vertical * 2);
}

public enum MessageDirection { Incoming, Outgoing, Unknown }

public sealed record OcrLine(string Text, double Confidence, ScreenRect Bounds);

public sealed record ChatMessage(
    string Id,
    string SessionId,
    MessageDirection Direction,
    string Text,
    double Confidence,
    ScreenRect Bounds,
    string? Sender = null);

public enum AnalysisState { Loading, Ready, LowConfidence, Failed, Unavailable }

public sealed record DecisionOption(string Label, int Probability);

/// <summary>
/// One labeled judgment with its distribution (e.g. 情绪状态 / 真实意图 / 怎么回). Highlighted
/// dimensions are the ones worth the user's attention first (marked with ★ in the card).
/// </summary>
public sealed record JudgmentDimension(string Label, IReadOnlyList<DecisionOption> Options, bool Highlight = false);

public sealed record DecisionCard(
    string MessageId,
    string Topic,
    string Question,
    IReadOnlyList<DecisionOption> Options,
    int RiskLevel,
    string RiskLabel,
    string RecommendedAction,
    int Confidence,
    AnalysisState State)
{
    /// <summary>Rich dimensions (潜台词 / 情绪 / 意图 / 关系 / 期待 / 怎么回). Empty = show Options only.</summary>
    public IReadOnlyList<JudgmentDimension> Dimensions { get; init; } = Array.Empty<JudgmentDimension>();

    /// <summary>Dimensions ordered for display: highlighted first, capped for a readable card.</summary>
    public IReadOnlyList<JudgmentDimension> VisibleDimensions(int max = 5) =>
        Dimensions.OrderByDescending(dimension => dimension.Highlight).Take(max).ToArray();
}

public enum CardPlacementKind { BelowMessage, BesideMessage, SideRail, Marker }

public sealed record CardLayout(
    string MessageId,
    ScreenRect Bounds,
    CardPlacementKind Placement,
    DecisionCard Card)
{
    /// <summary>The message being judged — shown on the card so it can be matched to the chat
    /// even after the group scrolls on.</summary>
    public string MessageText { get; init; } = string.Empty;

    /// <summary>Who sent it (group speaker, or the contact in a 1:1 chat); null when unknown.</summary>
    public string? Sender { get; init; }
}

public sealed record CapturedFrame(
    System.Windows.Media.Imaging.BitmapSource Image,
    ScreenRect WindowBounds,
    uint Dpi);

public sealed record Calibration(double X, double Y, double Width, double Height)
{
    public static readonly Calibration Default = new(0.24, 0.12, 0.74, 0.60);

    public ScreenRect ToScreenRect(ScreenRect window) => new(
        window.X + window.Width * X,
        window.Y + window.Height * Y,
        window.Width * Width,
        window.Height * Height);
}
