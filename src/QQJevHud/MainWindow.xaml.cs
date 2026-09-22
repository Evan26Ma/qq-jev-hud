using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using QQJevHud.Services;
using QQJevHud.Views;

namespace QQJevHud;

/// <summary>
/// Invisible message host. Visible surfaces are the QQ-anchored overlay, the choice panel, the home /
/// settings windows and the tray menu.
/// </summary>
public partial class MainWindow : Window, IDisposable
{
    private const int ToggleHotKeyId = 0x4A65;
    private readonly OverlayWindow _overlay = new();
    private readonly HudController _controller;
    private readonly Forms.NotifyIcon _tray;
    private readonly ChoiceWindow _choiceWindow;
    private HomeWindow? _homeWindow;
    private SettingsWindow? _settingsWindow;
    private bool _allowClose;
    private bool _disposed;

    public MainWindow()
    {
        InitializeComponent();
        _controller = new HudController(_overlay);
        _controller.StatusChanged += status => Dispatcher.InvokeAsync(() => StatusText.Text = status);
        _choiceWindow = new ChoiceWindow();
        _controller.ChoiceSetReady += set => Dispatcher.InvokeAsync(() => _choiceWindow.ShowChoiceSet(set));
        _choiceWindow.RegenerateRequested += message => _controller.RegenerateReplies(message);
        _tray = CreateTrayIcon();
        Closing += OnClosing;
        var handle = new WindowInteropHelper(this).EnsureHandle();
        if (HwndSource.FromHwnd(handle) is { } source)
        {
            source.AddHook(WindowMessageHook);
            NativeMethods.RegisterHotKey(handle, ToggleHotKeyId, NativeMethods.ModControl | NativeMethods.ModAlt, 0x4A);
        }
    }

    /// <summary>Starts analysis and shows the home page, which is the app's entry point.</summary>
    public void StartAutomaticAnalysis()
    {
        _ = _controller.StartCurrentChatAsync();
        ShowHome();
    }

    public void OpenSettingsWindow() => ShowSettings();

    private void ShowHome()
    {
        if (_homeWindow is { IsLoaded: true })
        {
            _homeWindow.Refresh();
            _homeWindow.Activate();
            return;
        }
        _homeWindow = new HomeWindow(_controller, ShowSettings);
        _homeWindow.Closed += (_, _) => _homeWindow = null;
        _homeWindow.Show();
    }

    private void ShowSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(_controller, new SettingsStore().Load());
        _settingsWindow.Closed += (_, _) => { _settingsWindow = null; _homeWindow?.Refresh(); };
        _settingsWindow.Show();
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开主界面", null, (_, _) => ShowHome());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("自动分析已开启", null, async (_, _) => await _controller.StartCurrentChatAsync());
        menu.Items.Add("暂停自动分析", null, (_, _) => _controller.Pause());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("重新校准聊天区域", null, (_, _) => _controller.OpenCalibration());
        menu.Items.Add("怎么回…", null, (_, _) => _choiceWindow.Show());
        menu.Items.Add("设置…", null, (_, _) => ShowSettings());
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
        _choiceWindow.Close();
        _settingsWindow?.Close();
        _homeWindow?.Close();
        _overlay.ClearCards();
        _overlay.Close();
        _controller.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
