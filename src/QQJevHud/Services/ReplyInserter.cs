using System.Runtime.InteropServices;
using System.Windows;
using Forms = System.Windows.Forms;

namespace QQJevHud.Services;

/// <summary>
/// Puts a chosen reply where the user can send it. <see cref="Copy"/> always works;
/// <see cref="FillIntoQq"/> is a best-effort paste into the QQ input box. **Sending is always
/// the user's action** — nothing here ever presses Enter.
/// </summary>
public static class ReplyInserter
{
    private static readonly QQWindowLocator Locator = new();

    public static void Copy(string text)
    {
        try { System.Windows.Clipboard.SetText(text); } catch { /* clipboard can be momentarily locked */ }
    }

    /// <summary>Copy the text, focus QQ and paste it. Returns false if QQ could not be focused.</summary>
    public static bool FillIntoQq(string text)
    {
        Copy(text);
        var qq = Locator.Find();
        if (qq is null) return false;
        if (!SetForegroundWindow(qq.Handle)) return false;
        Thread.Sleep(150);                       // let the chat input take focus
        try { Forms.SendKeys.SendWait("^v"); } catch { return false; }
        return true;
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
