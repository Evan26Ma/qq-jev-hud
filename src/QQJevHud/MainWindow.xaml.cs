using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using QQJevHud.Services;
using QQJevHud.Views;

namespace QQJevHud;

/// <summary>Invisible message host: the only visible UI is the QQ-anchored overlay and the tray menu.</summary>
public partial class MainWindow : Window, IDisposable
{
    private const int ToggleHotKeyId = 0x4A65;
    private readonly OverlayWindow _overlay = new();
    private readonly HudController _controller;
    private readonly Forms.NotifyIcon _tray;
    private readonly ReplyWindow _replyWindow;
    private bool _allowClose;
    private bool _disposed;

    public MainWindow()
    {
        InitializeComponent();
        _controller = new HudController(_overlay);
        _controller.StatusChanged += status => Dispatcher.InvokeAsync(() => StatusText.Text = status);
        _replyWindow = new ReplyWindow();
        _controller.ReplyDraftReady += draft => Dispatcher.InvokeAsync(() =>
        {
            _replyWindow.ShowDraft(draft);
            if (!_replyWindow.IsVisible) _replyWindow.Show();
        });
        _replyWindow.RegenerateRequested += message => _controller.RegenerateReplies(message);
        _tray = CreateTrayIcon();
        Closing += OnClosing;
        var handle = new WindowInteropHelper(this).EnsureHandle();
        if (HwndSource.FromHwnd(handle) is { } source)
        {
            source.AddHook(WindowMessageHook);
            NativeMethods.RegisterHotKey(handle, ToggleHotKeyId, NativeMethods.ModControl | NativeMethods.ModAlt, 0x4A);
        }
    }

    public void StartAutomaticAnalysis() => _ = _controller.StartCurrentChatAsync();

    public void OpenSettingsWindow() => _controller.OpenSettings();

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("自动分析已开启", null, async (_, _) => await _controller.StartCurrentChatAsync());
        menu.Items.Add("暂停自动分析", null, (_, _) => _controller.Pause());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("重新校准聊天区域", null, (_, _) => _controller.OpenCalibration());
        menu.Items.Add("候选回复…", null, (_, _) => _replyWindow.Show());
        menu.Items.Add("设置…", null, (_, _) => _controller.OpenSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApplication());
        return new Forms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "QQ Jev HUD（自动分析）",
            ContextMenuStrip = menu,
            Visible = true
        };
    }

    /// <summary>The app icon (app.ico) for the tray; falls back to the generic icon if unavailable.</summary>
    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(new Uri("app.ico", UriKind.Relative));
            if (resource is not null) return new System.Drawing.Icon(resource.Stream);
        }
        catch
        {
            // fall through to the stock icon
        }
        return System.Drawing.SystemIcons.Application;
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotkey && wParam.ToInt32() == ToggleHotKeyId)
        {
            _controller.Toggle();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
    }

    private void ExitApplication()
    {
        _allowClose = true;
        Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero) NativeMethods.UnregisterHotKey(handle, ToggleHotKeyId);
        _tray.Visible = false;
        _tray.Dispose();
        _overlay.ClearCards();
        _overlay.Close();
        _replyWindow.Close();
        _controller.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
