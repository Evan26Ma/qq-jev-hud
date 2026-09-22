using System.Windows;
using QQJevHud.Services;

namespace QQJevHud;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--set-typesafe-key", StringComparer.OrdinalIgnoreCase))
        {
            var key = Console.In.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                Shutdown(-1);
                return;
            }
            CredentialStore.SaveTypeSafeKey(key);
            Shutdown(0);
            return;
        }
        if (e.Args.Contains("--verify-typesafe", StringComparer.OrdinalIgnoreCase))
        {
            _ = VerifyTypeSafeAsync();
            return;
        }
        _mainWindow = new MainWindow();
        _mainWindow.Hide();
        _mainWindow.StartAutomaticAnalysis();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainWindow?.Dispose();
        base.OnExit(e);
    }

    private async Task VerifyTypeSafeAsync()
    {
        try
        {
            var card = await new Core.TypeSafeDecisionProvider().AnalyzeAsync(
                new Core.ChatMessage("health", "health", Core.MessageDirection.Incoming, "请判断这句话是否期待认真回应。", 1, new Core.ScreenRect()),
                Array.Empty<Core.ChatMessage>(), CancellationToken.None);
            Shutdown(card.State is Core.AnalysisState.Ready or Core.AnalysisState.LowConfidence ? 0 : 1);
        }
        catch { Shutdown(1); }
    }
}
