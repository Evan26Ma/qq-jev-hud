using System.Diagnostics;
using System.Text.Json;
using System.Windows.Media.Imaging;
using QQJevHud.Core;

namespace QQJevHud.Services;

public sealed class OcrWorkerClient : IOcrEngine
{
    // The first request also pays the one-time Paddle model load, so it gets a much
    // larger budget than warm inference before we treat the worker as wedged.
    private static readonly TimeSpan FirstResponseTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan WarmResponseTimeout = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private Process? _process;
    private string? _pythonPath;
    private string? _workerPath;
    private bool _warmedUp;

    public bool IsReady => _process is { HasExited: false };
    public string? LastError { get; private set; }

    public async Task<IReadOnlyList<OcrLine>> RecognizeAsync(BitmapSource image, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureStartedAsync(cancellationToken);
            var request = new WorkerRequest(Guid.NewGuid().ToString("N"), BitmapTools.ToPngBase64(image));
            await _process!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request));
            await _process.StandardInput.FlushAsync(cancellationToken);

            // Bound the wait so a wedged worker fails the frame (and auto-restarts) instead of
            // hanging the HUD forever. First call includes the model load; warm calls do not.
            var budget = _warmedUp ? WarmResponseTimeout : FirstResponseTimeout;
            using var watchdog = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            watchdog.CancelAfter(budget);

            WorkerResponse? response = null;
            var skipped = 0;
            try
            {
                // Paddle's Windows runtime can emit a few native diagnostic lines to stdout
                // before the JSON-lines worker responds. Ignore only non-JSON noise.
                for (var lineCount = 0; lineCount < 64 && response is null; lineCount++)
                {
                    var responseLine = await _process.StandardOutput.ReadLineAsync().WaitAsync(watchdog.Token);
                    if (string.IsNullOrWhiteSpace(responseLine)) continue;
                    try { response = JsonSerializer.Deserialize<WorkerResponse>(responseLine, _jsonOptions); }
                    catch (JsonException) { skipped++; }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The caller did not cancel us: the watchdog fired because the worker went quiet.
                throw new TimeoutException($"OCR 服务在 {budget.TotalSeconds:0} 秒内没有返回结果。");
            }
            if (response is null) throw new InvalidOperationException($"OCR 服务没有返回 JSON 结果（跳过 {skipped} 行非 JSON 输出）。");
            if (!response.Ok)
            {
                LastError = response.Error ?? "PaddleOCR 返回未知错误。";
                throw new InvalidOperationException(LastError);
            }
            LastError = null;
            _warmedUp = true;
            return response.Lines?.Select(item => new OcrLine(item.Text ?? string.Empty, item.Confidence,
                new ScreenRect(item.X, item.Y, item.Width, item.Height))).ToArray() ?? Array.Empty<OcrLine>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = ex.Message;
            Restart(); // 满足一次自动重启；下一帧会重新拉起进程。
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (IsReady) return;
        _pythonPath ??= ResolvePython();
        _workerPath ??= ResolveWorker();
        var info = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = $"\"{_workerPath}\" --worker",
            WorkingDirectory = Path.GetDirectoryName(_workerPath)!,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        info.Environment["PYTHONUTF8"] = "1";
        // The worker emits UTF-8 JSON (PYTHONUTF8=1). Read stdout/stderr as UTF-8 too;
        // the platform default (e.g. GBK) mis-decodes Chinese and corrupts the JSON.
        info.StandardOutputEncoding = System.Text.Encoding.UTF8;
        info.StandardErrorEncoding = System.Text.Encoding.UTF8;
        _process = Process.Start(info) ?? throw new InvalidOperationException("无法启动本地 OCR 服务。");
        _ = Task.Run(async () =>
        {
            try
            {
                var standardError = await _process.StandardError.ReadToEndAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(standardError)) LastError = standardError.Trim().Split('\n').Last();
            }
            catch (OperationCanceledException) { }
        }, CancellationToken.None);
        await Task.Delay(50, cancellationToken);
        if (_process.HasExited) throw new InvalidOperationException("OCR 服务启动失败，请先运行 setup.ps1。");
    }

    private static string ResolvePython()
    {
        var configured = Environment.GetEnvironmentVariable("QQJEVHUD_PYTHON");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        foreach (var root in CandidateRoots())
        {
            var candidate = Path.Combine(root, ".venv", "Scripts", "python.exe");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("找不到项目的 Python 环境。请先在项目根目录运行 setup.ps1。");
    }

    private static string ResolveWorker()
    {
        foreach (var root in CandidateRoots())
        {
            var candidate = Path.Combine(root, "ocr", "ocr_worker.py");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("找不到 ocr_worker.py。");
    }

    private static IEnumerable<string> CandidateRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (seen.Add(directory.FullName)) yield return directory.FullName;
            }
        }
    }

    private void Restart()
    {
        _warmedUp = false;
        if (_process is null) return;
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); } catch { }
        _process.Dispose();
        _process = null;
    }

    public ValueTask DisposeAsync()
    {
        Restart();
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed record WorkerRequest(string Id, string ImagePngBase64);
    private sealed record WorkerResponse(bool Ok, List<WorkerLine>? Lines, string? Error);
    private sealed record WorkerLine(string? Text, double Confidence, double X, double Y, double Width, double Height);
}
