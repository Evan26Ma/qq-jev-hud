using System.IO;
using System.Windows.Media.Imaging;
using QQJevHud.Services;

namespace QQJevHud.Tests;

/// <summary>
/// End-to-end check of the C# OcrWorkerClient against the real local PaddleOCR worker.
/// It feeds a synthetic text image (never real chat content) and asserts the JSON-lines
/// round-trip returns recognized lines. This is the component the handoff left unconfirmed.
/// </summary>
public sealed class OcrWorkerClientTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task RecognizeAsync_RoundTripsThroughLocalPaddleWorker_AndDetectsText()
    {
        var image = LoadTestImage();
        await using var ocr = new OcrWorkerClient();

        var lines = await ocr.RecognizeAsync(image, CancellationToken.None);

        Assert.Null(ocr.LastError);
        Assert.True(lines.Count >= 1, $"expected OCR to detect at least one line, got {lines.Count}");
        Assert.Contains(lines, line => !string.IsNullOrWhiteSpace(line.Text));
    }

    private static BitmapSource LoadTestImage()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "hello.png");
        using var stream = File.OpenRead(path);
        var decoded = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad)
            ?? throw new InvalidOperationException("failed to decode the test image");
        // Copy into a frozen WriteableBitmap. BitmapTools.ToPngBase64 calls
        // BitmapFrame.Create(source), which reads decoder-bound metadata that is
        // thread-affine to the decoding thread and throws off-thread. A frozen
        // non-decoder bitmap is safe to encode from any continuation thread.
        var frozen = new WriteableBitmap(decoded);
        frozen.Freeze();
        return frozen;
    }
}
