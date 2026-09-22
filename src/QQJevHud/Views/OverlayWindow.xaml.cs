using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Interop;
using QQJevHud.Core;
using QQJevHud.Services;

namespace QQJevHud.Views;

public partial class OverlayWindow : Window
{
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
        style |= NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow | NativeMethods.WsExTransparent;
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
        var scale = Math.Max(0.75, dpi / 96d);
        Left = windowBounds.Left / scale;
        Top = windowBounds.Top / scale;
        Width = windowBounds.Width / scale;
        Height = windowBounds.Height / scale;
        Cards.Clear();
        foreach (var layout in layouts)
        {
            Cards.Add(new OverlayCardViewModel
            {
                Card = layout.Card,
                Left = (layout.Bounds.Left - windowBounds.Left) / scale,
                Top = (layout.Bounds.Top - windowBounds.Top) / scale,
                Width = layout.Placement == CardPlacementKind.Marker ? 18 : layout.Bounds.Width,
                Height = layout.Placement == CardPlacementKind.Marker ? 18 : layout.Bounds.Height,
                IsMarker = layout.Placement == CardPlacementKind.Marker,
                ShowOptions = showOptions,
                ShowRisk = showRisk,
                ShowAdvice = showAdvice,
                MessageText = layout.MessageText,
                Sender = layout.Sender
            });
        }
        if (!IsVisible && Cards.Count > 0) Show();
    }

    public void ClearCards()
    {
        Cards.Clear();
        if (IsVisible) Hide();
    }

    public void ShowStatusCard(ScreenRect windowBounds, uint dpi, string message)
    {
        var scale = Math.Max(0.75, dpi / 96d);
        Left = windowBounds.Left / scale;
        Top = windowBounds.Top / scale;
        Width = windowBounds.Width / scale;
        Height = windowBounds.Height / scale;
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
            Left = Math.Max(8, Math.Min(windowBounds.Width - 296, windowBounds.Width * 0.60) / scale),
            Top = Math.Clamp(windowBounds.Height * 0.14, 96d, 132d) / scale,
            Width = 280,
            Height = 112,
            IsMarker = false,
            ShowOptions = true,
            ShowRisk = true,
            ShowAdvice = true
        });
        if (!IsVisible) Show();
    }
}
