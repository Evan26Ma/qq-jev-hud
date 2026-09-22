using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QQJevHud.Core;

namespace QQJevHud.Services;

/// <summary>
/// Finds the region of a captured QQ window that actually holds the conversation — the message column
/// — instead of guessing with fixed fractions of the window. A fraction-based box lands on the session
/// list or the empty margin, which looks like it was drawn at random.
///
/// The signal is theme-independent: message bubbles are painted *brighter* than the chat background
/// (white on light grey, light grey on dark), so each row's median gives a local background estimate
/// and the brighter pixels are bubble content. Aggregating those rows by their median extent yields a
/// box that hugs the messages; isolated bright chrome (the "new messages" pill, a scrollbar) is
/// rejected because it occupies too few rows to shift a median.
/// </summary>
public static class ChatAreaDetector
{
    /// <summary>Fallbacks when the frame has nothing bubble-like (e.g. an empty conversation).</summary>
    private const double FallbackLeft = 0.32;
    private const double FallbackTop = 0.12;
    private const double FallbackRight = 0.70;
    private const double FallbackBottom = 0.82;

    /// <summary>A pixel counts as content when it is this much brighter than the row's background.</summary>
    private const int BrightnessDelta = 5;

    /// <summary>A row needs at least this share of content pixels before it votes.</summary>
    private const double RowContentShare = 0.03;

    /// <summary>
    /// A voting row's content must also span at least this share of the band. Message bubbles are wide;
    /// narrow bright chrome (the "new messages" pill, a scrollbar thumb) is rejected by this alone.
    /// </summary>
    private const double RowSpanShare = 0.12;

    /// <summary>
    /// …and it must be mostly solid, not scattered glyphs. A bubble is a filled block (density near 1);
    /// a line of anti-aliased text or a row of icons leaves large gaps and is rejected here. This is
    /// what keeps the header and the input bar from being mistaken for messages.
    /// </summary>
    private const double RowDensity = 0.55;

    /// <summary>Padding around the detected message column.</summary>
    private const int PadTop = 10;
    private const int PadBottom = 10;

    /// <summary>
    /// Avatars and their gutters sit immediately left of the bubbles; widening by this much turns the
    /// bubble column into the whole chat panel, which is what "the area being read" should look like.
    /// </summary>
    private const int AvatarColumn = 74;

    /// <summary>Keeps the right edge just inside the window instead of touching the frame.</summary>
    private const int RightInset = 8;

    public static ScreenRect Detect(BitmapSource image, ScreenRect windowBounds)
    {
        var width = image.PixelWidth;
        var height = image.PixelHeight;
        if (width < 80 || height < 80) return Fractional(windowBounds);

        var luminance = Luminance(image);
        var bandLeft = (int)(width * 0.18);
        var bandRight = (int)(width * 0.99);
        var band = bandRight - bandLeft;
        if (band < 40) return Fractional(windowBounds);

        var lefts = new List<int>();
        var firstRow = int.MaxValue;
        var lastRow = int.MinValue;
        var minimumContent = (int)(band * RowContentShare);
        var minimumSpan = (int)(band * RowSpanShare);

        for (var y = (int)(height * 0.06); y < (int)(height * 0.90); y++)
        {
            var background = RowMedian(luminance, y * width, bandLeft, bandRight);
            var threshold = background + BrightnessDelta;
            var minX = int.MaxValue;
            var maxX = int.MinValue;
            var count = 0;

            for (var x = bandLeft; x < bandRight; x++)
            {
                if (luminance[y * width + x] <= threshold) continue;
                count++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
            }

            if (count < minimumContent) continue;
            // Reject narrow bright chrome: only wide content counts as conversation.
            var span = maxX - minX;
            if (span < minimumSpan) continue;
            // Reject scattered bright pixels (text, icon rows): a bubble is a solid block.
            if (count < span * RowDensity) continue;
            lefts.Add(minX);
            if (y < firstRow) firstRow = y;
            if (y > lastRow) lastRow = y;
        }

        // Too few contributing rows means there is no conversation to hug (empty chat, or a dark
        // window with no bubbles yet) — the fixed fallback is safer than a tiny box.
        if (lefts.Count < 6) return Fractional(windowBounds);

        // Vertical extent comes from the content rows: the bubbles' own top and bottom are far more
        // reliable than hunting for the header and input-bar edges. Horizontal extent is the chat panel
        // itself — from just left of the avatars to the window's right edge.
        var left = Percentile(lefts, 0.10) - AvatarColumn;
        var right = width - RightInset;
        var top = firstRow - PadTop;
        var bottom = lastRow + PadBottom;

        left = Math.Max(0, left);
        top = Math.Max(0, top);
        right = Math.Min(width, right);
        bottom = Math.Min(height, bottom);

        if (right - left < width * 0.30 || bottom - top < height * 0.15) return Fractional(windowBounds);

        return new ScreenRect(windowBounds.Left + left, windowBounds.Top + top, right - left, bottom - top);
    }

    private static ScreenRect Fractional(ScreenRect window) => new(
        window.Left + window.Width * FallbackLeft,
        window.Top + window.Height * FallbackTop,
        window.Width * (FallbackRight - FallbackLeft),
        window.Height * (FallbackBottom - FallbackTop));

    /// <summary>Per-pixel luminance (0..255) for the whole frame.</summary>
    private static byte[] Luminance(BitmapSource image)
    {
        var width = image.PixelWidth;
        var height = image.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        image.CopyPixels(pixels, stride, 0);

        var result = new byte[width * height];
        for (var index = 0; index < result.Length; index++)
        {
            var offset = index * 4;
            result[index] = (byte)((pixels[offset] * 11 + pixels[offset + 1] * 59 + pixels[offset + 2] * 30) / 100);
        }
        return result;
    }

    /// <summary>Median luminance of one row segment — a robust estimate of that row's background.</summary>
    private static int RowMedian(byte[] luminance, int rowOffset, int from, int to)
    {
        var histogram = new int[256];
        for (var x = from; x < to; x++) histogram[luminance[rowOffset + x]]++;
        var half = (to - from) / 2;
        var running = 0;
        for (var value = 0; value < histogram.Length; value++)
        {
            running += histogram[value];
            if (running > half) return value;
        }
        return 245;
    }

    /// <summary>Value at the given percentile of a sample (0.5 = median).</summary>
    private static int Percentile(List<int> values, double percentile)
    {
        values.Sort();
        var index = (int)Math.Round((values.Count - 1) * Math.Clamp(percentile, 0, 1));
        return values[Math.Clamp(index, 0, values.Count - 1)];
    }
}
