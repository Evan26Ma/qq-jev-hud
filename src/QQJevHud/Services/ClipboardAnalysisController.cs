using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QQJevHud.Core;

namespace QQJevHud.Services;

public sealed record ProgramAnalysis(ChatMessage Message, DecisionCard Card);

/// <summary>
/// Independent analysis mode: it only reads content the user explicitly copied to the clipboard.
/// No QQ window discovery, hooks, injection, or foreground checks are involved.
/// </summary>
public sealed class ClipboardAnalysisController : IAsyncDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly IOcrEngine _ocr = new OcrWorkerClient();
    private readonly IChatDetector _detector = new ChatDetector();
    private readonly IDecisionProvider _decisions = DecisionProviderFactory.Create(new SettingsStore().Load());
    private string? _lastFingerprint;
    private bool _processing;

    public ClipboardAnalysisController()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(450) };
        _timer.Tick += async (_, _) => await InspectClipboardAsync(onlyWhenChanged: true);
    }

    public bool IsListening => _timer.IsEnabled;
    public event Action<string>? StatusChanged;
    public event Action<ProgramAnalysis>? AnalysisReady;

    public void StartListening()
    {
        _lastFingerprint = null;
        _timer.Start();
        SetStatus("正在监听剪贴板；复制 QQ 文字或截图即可分析。");
    }

    public void Stop()
    {
        _timer.Stop();
        _lastFingerprint = null;
        SetStatus("已暂停，不会读取剪贴板。");
    }

    public void Toggle()
    {
        if (IsListening) Stop();
        else StartListening();
    }

    public Task AnalyzeCurrentClipboardAsync() => InspectClipboardAsync(onlyWhenChanged: false);

    private async Task InspectClipboardAsync(bool onlyWhenChanged)
    {
        if (_processing) return;
        _processing = true;
        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                var text = TextNormalizer.Normalize(System.Windows.Clipboard.GetText());
                if (string.IsNullOrWhiteSpace(text))
                {
                    SetStatus("剪贴板里没有可识别的文字。");
                    return;
                }
                var fingerprint = TextNormalizer.Fingerprint("clipboard", MessageDirection.Incoming, text);
                if (onlyWhenChanged && string.Equals(_lastFingerprint, fingerprint, StringComparison.Ordinal)) return;
                _lastFingerprint = fingerprint;
                await AnalyzeMessageAsync(new ChatMessage(fingerprint, "clipboard", MessageDirection.Incoming, text, 1, new ScreenRect()));
                return;
            }

            if (System.Windows.Clipboard.ContainsImage())
            {
                var image = System.Windows.Clipboard.GetImage();
                if (image is null)
                {
                    SetStatus("无法读取剪贴板截图。");
                    return;
                }
                var fingerprint = "image-" + BitmapTools.FrameSignature(image)[..16];
                if (onlyWhenChanged && string.Equals(_lastFingerprint, fingerprint, StringComparison.Ordinal)) return;
                _lastFingerprint = fingerprint;
                await AnalyzeImageAsync(image, fingerprint);
                return;
            }

            SetStatus("请先复制一段文字，或把 QQ 截图复制到剪贴板。");
        }
        catch (Exception ex)
        {
            SetStatus($"本次识别失败：{ex.Message}");
        }
        finally { _processing = false; }
    }

    private async Task AnalyzeImageAsync(BitmapSource image, string fingerprint)
    {
        SetStatus("正在识别剪贴板截图…");
        var lines = await _ocr.RecognizeAsync(image, CancellationToken.None);
        var history = new ScreenRect(0, 0, image.PixelWidth, image.PixelHeight);
        var messages = _detector.Detect(lines, fingerprint, history).OrderBy(message => message.Bounds.Top).ToArray();
        var candidates = messages.Where(message => message.Direction == MessageDirection.Incoming).ToArray();
        var message = candidates.LastOrDefault() ?? messages.LastOrDefault();
        if (message is null)
        {
            SetStatus("截图里没有识别到可分析的消息文字。");
            return;
        }
        await AnalyzeMessageAsync(message, messages.TakeLast(10).ToArray());
    }

    private async Task AnalyzeMessageAsync(ChatMessage message, IReadOnlyList<ChatMessage>? context = null)
    {
        SetStatus("Jev 正在判断…");
        var card = await _decisions.AnalyzeAsync(message, context ?? new[] { message }, CancellationToken.None);
        AnalysisReady?.Invoke(new ProgramAnalysis(message, card));
        SetStatus("判断完成（本地模拟分析）。复制下一条内容可继续分析。");
    }

    private void SetStatus(string status) => StatusChanged?.Invoke(status);

    public async ValueTask DisposeAsync()
    {
        Stop();
        await _ocr.DisposeAsync();
    }
}
