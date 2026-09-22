using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QQJevHud.Core;

namespace QQJevHud.Services;

/// <summary>
/// Captures only the currently visible QQ window. The abstraction keeps a future Windows Graphics Capture backend swappable.
/// </summary>
public sealed class WindowCaptureService
{
    public CapturedFrame? Capture(QQWindow window)
    {
        if (!NativeMethods.GetWindowRect(window.Handle, out var nativeRect)) return null;
        var width = nativeRect.Right - nativeRect.Left;
        var height = nativeRect.Bottom - nativeRect.Top;
        if (width <= 0 || height <= 0) return null;

        var sourceDc = NativeMethods.GetWindowDC(window.Handle);
        if (sourceDc == IntPtr.Zero) return null;
        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previous = IntPtr.Zero;
        try
        {
            memoryDc = NativeMethods.CreateCompatibleDC(sourceDc);
            bitmap = NativeMethods.CreateCompatibleBitmap(sourceDc, width, height);
            previous = NativeMethods.SelectObject(memoryDc, bitmap);
            var printed = NativeMethods.PrintWindow(window.Handle, memoryDc, NativeMethods.PwRenderFullContent);
            if (!printed)
            {
                NativeMethods.BitBlt(memoryDc, 0, 0, width, height, sourceDc, 0, 0, NativeMethods.Srccopy);
            }

            var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return new CapturedFrame(source, new ScreenRect(nativeRect.Left, nativeRect.Top, width, height), window.Dpi == 0 ? 96 : window.Dpi);
        }
        finally
        {
            if (previous != IntPtr.Zero && memoryDc != IntPtr.Zero) NativeMethods.SelectObject(memoryDc, previous);
            if (bitmap != IntPtr.Zero) NativeMethods.DeleteObject(bitmap);
            if (memoryDc != IntPtr.Zero) NativeMethods.DeleteDC(memoryDc);
            NativeMethods.ReleaseDC(window.Handle, sourceDc);
        }
    }
}
