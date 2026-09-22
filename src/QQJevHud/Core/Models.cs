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
    ScreenRect Bounds);

public enum AnalysisState { Loading, Ready, LowConfidence, Failed, Unavailable }

public sealed record DecisionOption(string Label, int Probability);

public sealed record DecisionCard(
    string MessageId,
    string Topic,
    string Question,
    IReadOnlyList<DecisionOption> Options,
    int RiskLevel,
    string RiskLabel,
    string RecommendedAction,
    int Confidence,
    AnalysisState State);

public enum CardPlacementKind { BelowMessage, BesideMessage, SideRail, Marker }

public sealed record CardLayout(
    string MessageId,
    ScreenRect Bounds,
    CardPlacementKind Placement,
    DecisionCard Card);

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
