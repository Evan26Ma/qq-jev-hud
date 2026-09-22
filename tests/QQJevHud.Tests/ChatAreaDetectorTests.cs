using System.Windows.Media;
using System.Windows.Media.Imaging;
using QQJevHud.Core;
using QQJevHud.Services;

namespace QQJevHud.Tests;

/// <summary>
/// The read region must hug the real message column. A fraction-based box lands on the session list or
/// the empty margin, which is exactly what "the frame is drawn at random" looks like to a user.
/// </summary>
public sealed class ChatAreaDetectorTests
{
    private const int Width = 1200;
    private const int Height = 800;

    /// <summary>
    /// Renders a stand-in QQ window: a session list on the left, chat background, message bubbles
    /// brighter than that background, plus bright chrome (a notification pill) to the right.
    /// </summary>
    private static BitmapSource Frame(int background = 245, int bubble = 255, int sessionList = 245, bool bubbles = true)
    {
        var stride = Width * 4;
        var pixels = new byte[stride * Height];
        void Fill(int x0, int y0, int x1, int y1, int value)
        {
            for (var y = y0; y < y1; y++)
                for (var x = x0; x < x1; x++)
                {
                    var offset = y * stride + x * 4;
                    pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = (byte)value;
                    pixels[offset + 3] = 255;
                }
        }

        Fill(0, 0, Width, Height, background);
        Fill(0, 0, 360, Height, sessionList);                  // session list column
        if (bubbles)
        {
            // Message column: the bubbles' left edge sits around x=440, the right edge varies.
            Fill(440, 150, 700, 190, bubble);
            Fill(440, 260, 760, 300, bubble);
            Fill(440, 380, 660, 420, bubble);
            Fill(440, 500, 800, 540, bubble);
            Fill(440, 620, 690, 660, bubble);
        }
        Fill(1080, 100, 1180, 130, bubble);                     // "new messages" pill: bright but isolated
        var bitmap = BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static ScreenRect Window() => new(100, 50, Width, Height);

    [Fact]
    public void Detect_HugsTheChatPanel_NotTheWholeWindow()
    {
        var region = ChatAreaDetector.Detect(Frame(), Window());

        var left = region.Left - Window().Left;
        var right = region.Right - Window().Left;
        // The box starts inside the chat area (right of the session list), allowing for avatars …
        Assert.True(left > 260, $"left edge {left} should clear the session list");
        Assert.True(left < 400, $"left edge {left} should not sit on the bubbles themselves");
        // … and runs to the window's right edge (the chat panel), not stopping at the bubble edge.
        Assert.True(right > Width * 0.97, $"right edge {right} should reach the chat panel edge");
    }

    [Fact]
    public void Detect_IgnoresIsolatedChromeFarToTheRight()
    {
        var region = ChatAreaDetector.Detect(Frame(), Window());

        // The right edge is the chat panel bound, not something stretched by the bright pill.
        Assert.True((region.Right - Window().Left) > Width * 0.97);
    }

    [Fact]
    public void Detect_VerticalExtent_CoversTheBubbles()
    {
        var region = ChatAreaDetector.Detect(Frame(), Window());

        var top = region.Top - Window().Top;
        var bottom = region.Bottom - Window().Top;
        Assert.True(top <= 150 && top > 100, $"top {top} should pad the first bubble");
        Assert.True(bottom >= 660 && bottom < 740, $"bottom {bottom} should pad the last bubble");
    }

    [Fact]
    public void Detect_DarkTheme_StillFindsTheColumn()
    {
        // Dark theme: bubbles are lighter than a dark background, so the same rule applies.
        var region = ChatAreaDetector.Detect(Frame(background: 40, bubble: 70, sessionList: 35), Window());

        var left = region.Left - Window().Left;
        Assert.True(left > 360 && left < 470, $"left edge {left} should still clear the session list");
    }

    [Fact]
    public void Detect_NoBubbles_FallsBackToAFractionInsteadOfTinyBox()
    {
        // An empty conversation: no content rows, so the fixed fallback is used.
        var region = ChatAreaDetector.Detect(Frame(bubbles: false), Window());

        Assert.True(region.Width > Width * 0.2);
        Assert.True(region.Height > Height * 0.3);
        Assert.False(region.IsEmpty);
    }

    [Fact]
    public void Detect_EmptyChatWithHeaderAndInputBar_DoesNotStretchBetweenThem()
    {
        // Regression: with no messages, a header title and a row of input icons used to be mistaken for
        // content, producing a box spanning almost the whole window.
        var stride = Width * 4;
        var pixels = new byte[stride * Height];
        void Fill(int x0, int y0, int x1, int y1, int value)
        {
            for (var y = y0; y < y1; y++)
                for (var x = x0; x < x1; x++)
                {
                    var offset = y * stride + x * 4;
                    pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = (byte)value;
                    pixels[offset + 3] = 255;
                }
        }
        Fill(0, 0, Width, Height, 245);
        Fill(0, 0, 360, Height, 245);
        // Header title: dark text on the light panel (dark pixels, nothing bright).
        for (var x = 420; x < 800; x += 12) Fill(x, 55, x + 6, 75, 90);
        // Input bar icons along the bottom.
        for (var x = 400; x < 700; x += 36) Fill(x, 690, x + 20, 714, 120);

        var bitmap = BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();

        var region = ChatAreaDetector.Detect(bitmap, Window());

        // Neither the header (y≈55) nor the input bar (y≈690) may anchor the region: with no messages
        // the detector must fall back to its fixed fractions, not stretch between the chrome.
        var top = region.Top - Window().Top;
        var bottom = region.Bottom - Window().Top;
        Assert.True(top > Height * 0.06, $"top {top} should be the fallback, not the header");
        Assert.True(bottom < Height * 0.90, $"bottom {bottom} should be the fallback, not the input bar");
        Assert.True(top >= Height * 0.10 && top <= Height * 0.16, $"top {top} should be the fallback fraction");
        Assert.True(bottom >= Height * 0.78 && bottom <= Height * 0.86, $"bottom {bottom} should be the fallback fraction");
    }

    [Fact]
    public void Detect_TinyFrame_DoesNotThrow()
    {
        var stride = 40 * 4;
        var pixels = new byte[stride * 40];
        for (var index = 0; index < pixels.Length; index++) pixels[index] = 255;
        var small = BitmapSource.Create(40, 40, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        small.Freeze();

        var region = ChatAreaDetector.Detect(small, new ScreenRect(0, 0, 40, 40));

        Assert.False(region.IsEmpty);
    }
}
