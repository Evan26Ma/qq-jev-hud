using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QQJevHud.Core;

namespace QQJevHud.Services;

public static class BitmapTools
{
    public static BitmapSource Crop(BitmapSource source, ScreenRect absoluteBounds, ScreenRect cropBounds)
    {
        var left = Math.Max(0, (int)Math.Round(cropBounds.Left - absoluteBounds.Left));
        var top = Math.Max(0, (int)Math.Round(cropBounds.Top - absoluteBounds.Top));
        var width = Math.Min(source.PixelWidth - left, Math.Max(1, (int)Math.Round(cropBounds.Width)));
        var height = Math.Min(source.PixelHeight - top, Math.Max(1, (int)Math.Round(cropBounds.Height)));
        var bitmap = new CroppedBitmap(source, new Int32Rect(left, top, width, height));
        bitmap.Freeze();
        return bitmap;
    }

    public static string ToPngBase64(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return Convert.ToBase64String(stream.ToArray());
    }

    public static string FrameSignature(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var scaleX = Math.Max(1, converted.PixelWidth / 32);
        var scaleY = Math.Max(1, converted.PixelHeight / 18);
        var bytes = new byte[32 * 18];
        var i = 0;
        var buffer = new byte[4];
        for (var y = 0; y < converted.PixelHeight && i < bytes.Length; y += scaleY)
        {
            for (var x = 0; x < converted.PixelWidth && i < bytes.Length; x += scaleX)
            {
                converted.CopyPixels(new Int32Rect(x, y, 1, 1), buffer, 4, 0);
                bytes[i++] = (byte)((buffer[0] * 11 + buffer[1] * 59 + buffer[2] * 30) / 100);
            }
        }
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
