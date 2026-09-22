using System.Windows.Threading;
using QQJevHud.Core;
using QQJevHud.Views;

namespace QQJevHud.Services;

public sealed class HudController : IAsyncDisposable
{
    private readonly OverlayWindow _overlay;
    private readonly QQWindowLocator _windowLocator = new();
    private readonly WindowCaptureService _capture = new();
    private readonly IOcrEngine _ocr = new OcrWorkerClient();
    private readonly IChatDetector _detector = new ChatDetector();
    // QQJEVHUD_MOCK=1 swaps in a local, offline decision provider (no network call),
    // for consent-safe demos and development without a TypeSafe key.
    private readonly IDecisionProvider _decisions =
        string.Equals(Environment.GetEnvironmentVariable("QQJEVHUD_MOCK"), "1", StringComparison.Ordinal)
            ? new MockDecisionProvider()
            : new TypeSafeDecisionProvider();
    private readonly OverlayLayoutEngine _layout = new();
    private readonly CalibrationStore _calibrationStore = new();
    private readonly FrameDebouncer _frameDebouncer = new(TimeSpan.FromMilliseconds(950));
    private readonly DispatcherTimer _timer;
    private readonly HashSet<string> _seenIncoming = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (ChatMessage Message, DecisionCard Card)> _cards = new(StringComparer.Ordinal);
    private string? _sessionId;
    private string? _lastRuntimeState;
    private bool _baselineTaken;
    private bool _polling;
    private bool _enabled;

    public HudController(OverlayWindow overlay)
    {
        _overlay = overlay;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(350) };
        _timer.Tick += async (_, _) => await PollAsync();
    }

    public bool IsEnabled => _enabled;
    public event Action<string>? StatusChanged;

    public async Task StartCurrentChatAsync()
    {
        _enabled = true;
        ResetSession();
        _timer.Start();
        SetStatus("正在等待 QQ 聊天窗口…");
        await PollAsync();
    }

    public void Pause()
    {
        _enabled = false;
        _timer.Stop();
        ResetSession();
        _overlay.ClearCards();
        SetStatus("已暂停，不会读取 QQ 画面。");
    }

    public void Toggle()
    {
        if (_enabled) Pause();
        else _ = StartCurrentChatAsync();
    }

    public void OpenCalibration()
    {
        var qq = _windowLocator.Find();
        if (qq is null)
        {
            SetStatus("未找到 QQ，请先打开一个聊天窗口。");
            return;
        }
        var frame = _capture.Capture(qq);
        if (frame is null)
        {
            SetStatus("无法截取 QQ 窗口，请将 QQ 显示在屏幕上。");
            return;
        }
        var selector = new CalibrationWindow(frame.Image, frame.WindowBounds, frame.Dpi, _calibrationStore.Load());
        selector.CalibrationSaved += calibration =>
        {
            _calibrationStore.Save(calibration);
            ResetSession();
            SetStatus("聊天区域已保存；重新开始分析后生效。");
        };
        selector.Show();
    }

    private async Task PollAsync()
    {
        if (!_enabled || _polling) return;
        _polling = true;
        try
        {
            var qq = _windowLocator.Find();
            if (qq is null)
            {
                RecordRuntimeState("qq-not-found");
                _overlay.ClearCards();
                SetStatus("未找到 QQ。打开 QQ 后会自动继续等待。");
                return;
            }
            var qqIsForeground = QQWindowLocator.IsForeground(qq);
            RecordRuntimeState(qqIsForeground ? "qq-foreground" : "qq-background");
            if (qqIsForeground && !_baselineTaken && !_overlay.IsVisible)
            {
                _overlay.ShowStatusCard(qq.Bounds, qq.Dpi, "正在识别当前聊天…");
            }
            var frame = _capture.Capture(qq);
            if (frame is null)
            {
                RecordRuntimeState("capture-unavailable");
                return;
            }
            var calibration = _calibrationStore.Load();
            var historyBounds = calibration.ToScreenRect(frame.WindowBounds);
            var history = BitmapTools.Crop(frame.Image, frame.WindowBounds, historyBounds);
            var signature = BitmapTools.FrameSignature(history);
            if (!_frameDebouncer.Observe(signature, DateTimeOffset.UtcNow))
            {
                RecordRuntimeState("frame-settling");
                return;
            }

            var headerBounds = new ScreenRect(
                frame.WindowBounds.Left + frame.WindowBounds.Width * 0.24,
                frame.WindowBounds.Top + 4,
                frame.WindowBounds.Width * 0.72,
                Math.Min(62, frame.WindowBounds.Height * 0.12));
            var newSessionId = "s-" + BitmapTools.FrameSignature(BitmapTools.Crop(frame.Image, frame.WindowBounds, headerBounds))[..16];
            if (!string.Equals(_sessionId, newSessionId, StringComparison.Ordinal))
            {
                ResetSession();
                _sessionId = newSessionId;
                SetStatus("已识别当前会话；正在建立消息基线…");
            }

            IReadOnlyList<OcrLine> rawLines;
            try { rawLines = await _ocr.RecognizeAsync(history, CancellationToken.None); }
            catch (Exception ex)
            {
                RecordRuntimeState("ocr-unavailable:" + DiagnosticCode(ex.Message));
                SetStatus($"OCR 暂不可用：{ex.Message}");
                return;
            }
            var lines = rawLines.Select(line => line with
            {
                Bounds = new ScreenRect(line.Bounds.Left + historyBounds.Left, line.Bounds.Top + historyBounds.Top, line.Bounds.Width, line.Bounds.Height)
            }).ToArray();
            var messages = _detector.Detect(lines, _sessionId!, historyBounds).OrderBy(message => message.Bounds.Top).ToArray();
            var incoming = messages.Where(message => message.Direction == MessageDirection.Incoming).ToArray();

            if (!_baselineTaken)
            {
                foreach (var message in incoming) _seenIncoming.Add(message.Id);
                _baselineTaken = true;
                RecordRuntimeState("baseline-ready");
                SetStatus("当前聊天已就绪；新收到的消息会显示 Jev 判断。");
                if (qqIsForeground) _overlay.ShowStatusCard(frame.WindowBounds, frame.Dpi, "正在观察当前聊天的新消息…");
                return;
            }

            var context = messages.TakeLast(10).ToArray();
            foreach (var message in incoming.Where(message => _seenIncoming.Add(message.Id)))
            {
                var decision = await _decisions.AnalyzeAsync(message, context, CancellationToken.None);
                _cards[message.Id] = (message, decision);
            }

            var visible = incoming.Select(message => message.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var stale in _cards.Keys.Where(id => !visible.Contains(id)).ToArray()) _cards.Remove(stale);
            var currentCards = _cards.Values
                .OrderByDescending(item => item.Message.Bounds.Top)
                .Take(4)
                .ToArray();
            if (currentCards.Length == 0)
            {
                RecordRuntimeState("waiting-for-incoming-message");
                if (qqIsForeground) _overlay.ShowStatusCard(frame.WindowBounds, frame.Dpi, "正在等待新的左侧消息…");
                else _overlay.ClearCards();
            }
            else if (qqIsForeground)
            {
                RecordRuntimeState("cards-visible");
                _overlay.ShowLayouts(frame.WindowBounds, frame.Dpi, _layout.Arrange(currentCards, historyBounds));
            }
            else
            {
                RecordRuntimeState("cards-hidden-qq-background");
                // Continue recognizing the locked QQ process, but never place a topmost card over another app.
                _overlay.ClearCards();
            }
            SetStatus(currentCards.Length == 0 ? "等待新的对方消息…" : "Jev 判断已显示。");
        }
        catch (Exception ex)
        {
            RecordRuntimeState($"poll-failed:{ex.GetType().Name}");
            SetStatus("识别遇到异常，正在自动重试…");
        }
        finally { _polling = false; }
    }

    private void RecordRuntimeState(string state)
    {
        if (string.Equals(_lastRuntimeState, state, StringComparison.Ordinal)) return;
        _lastRuntimeState = state;
        HudDiagnostics.Record(state);
    }

    private static string DiagnosticCode(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail)) return "unknown";
        var normalized = detail.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= 180 ? normalized : normalized[..180];
    }

    private void ResetSession()
    {
        _sessionId = null;
        _baselineTaken = false;
        _seenIncoming.Clear();
        _cards.Clear();
        _frameDebouncer.Reset();
    }

    private void SetStatus(string text) => StatusChanged?.Invoke(text);

    public async ValueTask DisposeAsync()
    {
        Pause();
        await _ocr.DisposeAsync();
    }
}
