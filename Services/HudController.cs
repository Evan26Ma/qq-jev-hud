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
    // Resolved by RefreshProvider() on settings-save / analysis start; the factory keeps
    // QQJEVHUD_MOCK=1 as the highest-priority override for offline demos.
    private IDecisionProvider _decisions = null!;
    private readonly OverlayLayoutEngine _layout = new();
    private readonly CalibrationStore _calibrationStore = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly ContactNoteStore _noteStore = new();
    private AppSettings _settings = AppSettings.Default;
    private OpenAiReplyGenerator? _replyGenerator;
    private readonly CandidateEvaluator _evaluator = new();
    private IReadOnlyList<ChatMessage> _lastContext = Array.Empty<ChatMessage>();
    private string? _contactNotes;
    private string? _contactName;

    /// <summary>Group chats carry a member count in the title (e.g. "群名 (288)"); 1:1 chats do not.</summary>
    private static bool IsGroupChat(string? sessionTitle) =>
        !string.IsNullOrWhiteSpace(sessionTitle) && System.Text.RegularExpressions.Regex.IsMatch(sessionTitle, @"[(（]\s*\d+\s*[)）]");

    /// <summary>How much of the visible conversation is handed to the model as context.</summary>
    private const int MaxContextMessages = 60;
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
        RefreshProvider();
    }

    public bool IsEnabled => _enabled;
    public event Action<string>? StatusChanged;

    public async Task StartCurrentChatAsync()
    {
        _enabled = true;
        RefreshProvider();
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

    /// <summary>Applies settings saved from the UI: persist, swap the decision provider, restart cleanly.</summary>
    public void ApplySettings(AppSettings settings)
    {
        _settingsStore.Save(settings);
        RefreshProvider();
        ResetSession();
        SetStatus("设置已保存；重新分析后生效。");
    }

    private void RefreshProvider()
    {
        _settings = _settingsStore.Load().Normalize();
        _decisions = DecisionProviderFactory.Create(_settings);
        _replyGenerator = new OpenAiReplyGenerator(_settings.BaseUrl, _settings.Model);
        _overlay.Opacity = _settings.CardOpacity;
    }

    /// <summary>Redrafts the choices for a message (used when the card asks to regenerate).</summary>
    public void RegenerateReplies(ChatMessage message)
    {
        _overlay.MarkWaiting(message.Id);
        _ = BuildChoicesAsync(message, _lastContext);
    }

    /// <summary>
    /// The heart of the flow: judge the message, draft a few candidate replies, then ask Jev what each
    /// one would lead to — and hand the whole thing to the choice panel as one set. Never blocks the
    /// analysis loop.
    /// </summary>
    private async Task BuildChoicesAsync(ChatMessage message, IReadOnlyList<ChatMessage> context, DecisionCard? known = null)
    {
        _lastContext = context;
        try
        {
            var safeMessage = _settings.RedactSensitive ? message with { Text = SensitiveRedactor.Redact(message.Text) } : message;
            var safeContext = _settings.RedactSensitive
                ? context.Select(item => item with { Text = SensitiveRedactor.Redact(item.Text) }).ToArray()
                : context;

            var card = known ?? await _decisions.AnalyzeAsync(safeMessage, safeContext, CancellationToken.None, _contactNotes);
            if (!_settings.GenerateReplies || _replyGenerator is null)
            {
                _overlay.ApplyChoices(new ChoiceSet(message.Id, message.Text, message.Sender, card, Array.Empty<ChoiceItem>()));
                return;
            }

            var drafts = await _replyGenerator.GenerateAsync(safeMessage, safeContext, _settings.ResolveTones(), _contactNotes, CancellationToken.None);
            if (drafts.Count == 0)
            {
                _overlay.ApplyChoices(new ChoiceSet(message.Id, message.Text, message.Sender, card, Array.Empty<ChoiceItem>()));
                return;
            }

            // Sharpen each candidate with its predicted consequence before showing them.
            var outcomes = await _evaluator.EvaluateAsync(safeMessage, safeContext,
                drafts.Select(draft => draft.Text).ToArray(), _contactNotes, CancellationToken.None);
            var choices = drafts.Select((draft, index) => new ChoiceItem(
                index + 1,
                draft.Text,
                draft.ToneName,
                index < outcomes.Count ? outcomes[index] : null)).ToArray();
            _overlay.ApplyChoices(new ChoiceSet(message.Id, message.Text, message.Sender, card, choices));
        }
        catch
        {
            // choices are an enhancement; never disturb the HUD
        }
    }

    /// <summary>Opens the control panel / settings window.</summary>
    public void OpenSettings() => new SettingsWindow(this, _settingsStore.Load()).Show();

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

            // Follow the window: the overlay is re-pinned on every poll, so cards travel with QQ as it
            // is moved or resized instead of waiting for the next settled frame.
            _overlay.FollowWindow(qq.Bounds, qq.Dpi);

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
                _contactNotes = await LoadContactNotesAsync(frame);
                _contactName = await LoadSessionTitleAsync(frame);
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
            var messages = _detector.Detect(lines, _sessionId!, historyBounds, IsGroupChat(_contactName)).OrderBy(message => message.Bounds.Top).ToArray();
            // In a 1:1 chat the session title is the contact, so label every incoming message with it.
            if (!IsGroupChat(_contactName) && !string.IsNullOrWhiteSpace(_contactName))
                messages = messages.Select(message => message.Direction == MessageDirection.Incoming && message.Sender is null
                    ? message with { Sender = _contactName }
                    : message).ToArray();
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

            // The whole visible conversation is the context handed to the model — not just the last few
            // incoming lines — so it can read the exchange the way a person would.
            var context = messages.TakeLast(MaxContextMessages).ToArray();
            foreach (var message in incoming.Where(message => _seenIncoming.Add(message.Id)))
            {
                // Redact sensitive tokens before any text leaves for the decision provider.
                var safeMessage = _settings.RedactSensitive ? message with { Text = SensitiveRedactor.Redact(message.Text) } : message;
                var safeContext = _settings.RedactSensitive
                    ? context.Select(item => item with { Text = SensitiveRedactor.Redact(item.Text) }).ToArray()
                    : context;
                var decision = await _decisions.AnalyzeAsync(safeMessage, safeContext, CancellationToken.None, _contactNotes);
                _cards[message.Id] = (message, decision);
                // Judgment is on screen already; the replies are generated next and land in the card.
                _ = BuildChoicesAsync(message, context, decision);
            }

            var visible = incoming.Select(message => message.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var stale in _cards.Keys.Where(id => !visible.Contains(id)).ToArray()) _cards.Remove(stale);
            var currentCards = _cards.Values
                .OrderByDescending(item => item.Message.Bounds.Top)
                .Take(_settings.MaxCards)
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
                _overlay.ShowLayouts(frame.WindowBounds, frame.Dpi, _layout.Arrange(currentCards, historyBounds),
                    _settings.ShowIntent, _settings.ShowRisk, _settings.ShowAdvice);
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

    /// <summary>
    /// Reads the chat header (best effort) to find the contact name, then loads that contact's
    /// background note from notes/&lt;联系人&gt;.md. Failures are silent — notes are an enhancement.
    /// </summary>
    private async Task<string?> LoadContactNotesAsync(CapturedFrame frame)
    {
        var name = await LoadSessionTitleAsync(frame);
        return string.IsNullOrWhiteSpace(name) ? null : _noteStore.Load(name);
    }

    /// <summary>OCR of the chat header title: the contact / group name (null when unreadable).</summary>
    private async Task<string?> LoadSessionTitleAsync(CapturedFrame frame)
    {
        try
        {
            var headerBounds = new ScreenRect(
                frame.WindowBounds.Left + frame.WindowBounds.Width * 0.26,
                frame.WindowBounds.Top + 4,
                frame.WindowBounds.Width * 0.44,
                Math.Min(46, frame.WindowBounds.Height * 0.09));
            var crop = BitmapTools.Crop(frame.Image, frame.WindowBounds, headerBounds);
            var lines = await _ocr.RecognizeAsync(crop, CancellationToken.None);
            return lines
                .Where(line => line.Confidence >= 0.6 && !string.IsNullOrWhiteSpace(line.Text))
                .OrderBy(line => line.Bounds.Top)
                .ThenBy(line => line.Bounds.Left)
                .Select(line => TextNormalizer.Normalize(line.Text))
                .FirstOrDefault(text => text.Length is > 0 and < 40);
        }
        catch
        {
            return null;
        }
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
        _contactName = null;
        _contactNotes = null;
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
