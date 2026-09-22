namespace QQJevHud.Core;

public interface IOcrEngine : IAsyncDisposable
{
    bool IsReady { get; }
    string? LastError { get; }
    Task<IReadOnlyList<OcrLine>> RecognizeAsync(
        System.Windows.Media.Imaging.BitmapSource image,
        CancellationToken cancellationToken);
}

public interface IChatDetector
{
    IReadOnlyList<ChatMessage> Detect(
        IReadOnlyList<OcrLine> lines,
        string sessionId,
        ScreenRect historyBounds);
}

public interface IDecisionProvider
{
    Task<DecisionCard> AnalyzeAsync(
        ChatMessage message,
        IReadOnlyList<ChatMessage> context,
        CancellationToken cancellationToken);
}
