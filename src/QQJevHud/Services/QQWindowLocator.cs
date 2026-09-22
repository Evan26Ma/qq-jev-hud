using System.Diagnostics;
using System.Text;
using QQJevHud.Core;

namespace QQJevHud.Services;

public sealed record QQWindow(IntPtr Handle, int ProcessId, ScreenRect Bounds, uint Dpi);

public sealed class QQWindowLocator
{
    public QQWindow? Find()
    {
        var qqProcessIds = Process.GetProcessesByName("QQ").Select(process => process.Id).ToHashSet();
        if (qqProcessIds.Count == 0) return null;

        QQWindow? match = null;
        NativeMethods.EnumWindows((handle, _) =>
        {
            if (!NativeMethods.IsWindowVisible(handle) || NativeMethods.IsIconic(handle)) return true;
            NativeMethods.GetWindowThreadProcessId(handle, out var processId);
            if (!qqProcessIds.Contains((int)processId)) return true;

            var className = new StringBuilder(256);
            NativeMethods.GetClassName(handle, className, className.Capacity);
            if (!string.Equals(className.ToString(), "Chrome_WidgetWin_1", StringComparison.Ordinal)) return true;
            if (!NativeMethods.GetWindowRect(handle, out var rect)) return true;

            var bounds = new ScreenRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            if (bounds.Width < 480 || bounds.Height < 360) return true;
            match = new QQWindow(handle, (int)processId, bounds, NativeMethods.GetDpiForWindow(handle));
            return false;
        }, IntPtr.Zero);

        return match;
    }

    public static bool IsForeground(QQWindow window) => NativeMethods.GetForegroundWindow() == window.Handle;
}
