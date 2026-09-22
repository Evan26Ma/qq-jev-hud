using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using QQJevHud.Core;
using QQJevHud.Services;

namespace QQJevHud.Views;

public partial class OverlayWindow : Window
{
    /// <summary>Cards kept as the same view-model instances across polls, so expansion survives redraws.</summary>
    private readonly Dictionary<string, OverlayCardViewModel> _cache = new(StringComparer.Ordinal);

    public ObservableCollection<OverlayCardViewModel> Cards { get; } = new();

    public OverlayWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64();
        // WS_EX_TRANSPARENT is deliberately NOT set: the card must receive clicks to expand and pick a
        // reply. Transparent pixels of a layered window stay click-through on their own, so the chat
        // behind the overlay remains fully usable.
        style |= NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow;
        style &= ~NativeMethods.WsExTransparent;
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, new IntPtr(style));
    }

    /// <summary>
    /// Rebuilds the card set. One card per judgment, positioned beside its message — but because a
    /// fast-moving group scrolls out from under the overlay, each card also quotes the message it
    /// judges (and who said it) so it can still be matched to the chat.
    /// </summary>
    public void ShowLayouts(ScreenRect windowBounds, uint dpi, IReadOnlyList<CardLayout> layouts,
        bool showOptions, bool showRisk, bool showAdvice)
    {
        ApplyBounds(windowBounds, dpi);

        var live = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<OverlayCardViewModel>();
        foreach (var layout in layouts)
        {
            live.Add(layout.Card.MessageId);
            // Reuse the existing view-model for this message so an expanded card stays expanded.
            if (_cache.TryGetValue(layout.Card.MessageId, out var existing))
            {
                ordered.Add(existing);
                continue;
            }
            var model = new OverlayCardViewModel
            {
                Card = layout.Card,
                Left = (layout.Bounds.Left - windowBounds.Left) / ScaleOf(dpi),
                Top = (layout.Bounds.Top - windowBounds.Top) / ScaleOf(dpi),
                Width = layout.Placement == CardPlacementKind.Marker ? 18 : layout.Bounds.Width,
                BaseHeight = layout.Placement == CardPlacementKind.Marker ? 18 : layout.Bounds.Height,
                IsMarker = layout.Placement == CardPlacementKind.Marker,
                ShowOptions = showOptions,
                ShowRisk = showRisk,
                ShowAdvice = showAdvice,
                MessageText = layout.MessageText,
                Sender = layout.Sender
            };
            if (!model.IsMarker) model.MarkWaiting();
            _cache[layout.Card.MessageId] = model;
            ordered.Add(model);
        }

        foreach (var stale in _cache.Keys.Where(id => !live.Contains(id)).ToArray()) _cache.Remove(stale);

        Cards.Clear();
        foreach (var model in ordered) Cards.Add(model);
        if (!IsVisible && Cards.Count > 0) Show();
    }

    /// <summary>Attaches the generated replies to the card for that message, without rebuilding it.</summary>
    public void ApplyChoices(ChoiceSet set)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => ApplyChoices(set));
            return;
        }
        if (_cache.TryGetValue(set.MessageId, out var model)) model.ApplyChoices(set);
    }

    /// <summary>Marks a message's card as waiting for choices (used when regeneration starts).</summary>
    public void MarkWaiting(string messageId)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => MarkWaiting(messageId));
            return;
        }
        if (_cache.TryGetValue(messageId, out var model)) model.MarkWaiting();
    }

    /// <summary>Moves the overlay onto the QQ window without touching the cards (called every poll).</summary>
    public void FollowWindow(ScreenRect windowBounds, uint dpi)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => FollowWindow(windowBounds, dpi));
            return;
        }
        ApplyBounds(windowBounds, dpi);
    }

    private void ApplyBounds(ScreenRect windowBounds, uint dpi)
    {
        var scale = ScaleOf(dpi);
        Left = windowBounds.Left / scale;
        Top = windowBounds.Top / scale;
        Width = windowBounds.Width / scale;
        Height = windowBounds.Height / scale;
    }

    private static double ScaleOf(uint dpi) => Math.Max(0.75, dpi / 96d);

    public void ClearCards()
    {
        _cache.Clear();
        Cards.Clear();
        if (IsVisible) Hide();
    }

    /// <summary>Click routing for the overlay: pick a reply, or expand/collapse a card.</summary>
    private void OnOverlayClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;

        // A reply row wins over the card body — it is the innermost hit.
        var choice = FindDataContext<ChoiceRowViewModel>(source);
        if (choice is not null && FindAncestorNamed(source, "ChoiceRow") is not null)
        {
            var filled = ReplyInserter.FillIntoQq(choice.Text);
            FindDataContext<OverlayCardViewModel>(source)?.SetStatus(
                filled ? "已填入 QQ 输入框 —— 确认后再点发送" : "QQ 未能聚焦，已复制；请在 QQ 里 Ctrl+V");
            e.Handled = true;
            return;
        }

        var card = FindDataContext<OverlayCardViewModel>(source);
        if (card is null || !card.HasChoices) return;
        card.IsExpanded = !card.IsExpanded;
        e.Handled = true;
    }

    private static T? FindDataContext<T>(DependencyObject? node) where T : class
    {
        while (node is not null)
        {
            if (node is FrameworkElement { DataContext: T match }) return match;
            node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);
        }
        return null;
    }

    private static DependencyObject? FindAncestorNamed(DependencyObject? node, string name)
    {
        while (node is not null)
        {
            if (node is FrameworkElement element && element.Name == name) return node;
            node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);
        }
        return null;
    }

    public void ShowStatusCard(ScreenRect windowBounds, uint dpi, string message)
    {
        ApplyBounds(windowBounds, dpi);
        _cache.Clear();
        Cards.Clear();
        Cards.Add(new OverlayCardViewModel
        {
            Card = new DecisionCard(
                "hud-status",
                "已锁定 QQ",
                message,
                Array.Empty<DecisionOption>(),
                0,
                "自动分析已开启",
                "收到新消息后将显示判断",
                100,
                AnalysisState.Ready),
            // Keep the transient status indicator inside the chat canvas, away from QQ's header controls and member rail.
            Left = Math.Max(8, Math.Min(windowBounds.Width - 296, windowBounds.Width * 0.60) / ScaleOf(dpi)),
            Top = Math.Clamp(windowBounds.Height * 0.14, 96d, 132d) / ScaleOf(dpi),
            Width = 280,
            BaseHeight = 112,
            IsMarker = false,
            ShowOptions = true,
            ShowRisk = true,
            ShowAdvice = true
        });
        if (!IsVisible) Show();
    }
}
