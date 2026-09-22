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
        if (e.Args.Contains("--set-openai-key", StringComparer.OrdinalIgnoreCase))
        {
            var key = Console.In.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                Shutdown(-1);
                return;
            }
            CredentialStore.SaveOpenAiKey(key);
            Shutdown(0);
            return;
        }
        if (e.Args.Contains("--verify-typesafe", StringComparer.OrdinalIgnoreCase))
        {
            _ = VerifyTypeSafeAsync();
            return;
        }
        if (e.Args.Contains("--preview-card", StringComparer.OrdinalIgnoreCase))
        {
            ShowPreviewCard();
            return;
        }
        if (e.Args.Contains("--preview-replies", StringComparer.OrdinalIgnoreCase))
        {
            ShowPreviewReplies();
            return;
        }
        _mainWindow = new MainWindow();
        _mainWindow.Hide();
        _mainWindow.StartAutomaticAnalysis();
        if (e.Args.Contains("--open-settings", StringComparer.OrdinalIgnoreCase))
            _mainWindow.OpenSettingsWindow();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainWindow?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Renders a sample rich card in an overlay so the layout can be eyeballed without QQ.
    /// Development aid: <c>QQJevHud.exe --preview-card</c>.
    /// </summary>
    private void ShowPreviewCard()
    {
        var overlay = new Views.OverlayWindow();
        var message = new Core.ChatMessage("preview", "preview", Core.MessageDirection.Incoming,
            "我昨天买的M7五级弹，4400一发，怕是有点难赚", 1, new Core.ScreenRect(150, 240, 320, 56), "零");
        var card = new Core.MockDecisionProvider()
            .AnalyzeAsync(message, Array.Empty<Core.ChatMessage>(), CancellationToken.None)
            .GetAwaiter().GetResult();

        var area = new Core.ScreenRect(60, 60, 940, 720);
        var layouts = new Core.OverlayLayoutEngine().Arrange(new[] { (message, card) }, area);
        overlay.ShowLayouts(area, 96, layouts, showOptions: true, showRisk: true, showAdvice: true);

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(120) };
        timer.Tick += (_, _) => { overlay.Close(); Shutdown(); };
        timer.Start();
    }

    /// <summary>Shows the reply panel with sample candidates: <c>QQJevHud.exe --preview-replies</c>.</summary>
    private void ShowPreviewReplies()
    {
        var window = new Views.ReplyWindow();
        window.Closed += (_, _) => Shutdown();
        var draft = new Core.ReplyDraft("preview", "你今天怎么都没理我？", new[]
        {
            new Core.ReplyGroup("高情商话术", new[]
            {
                new Core.ReplyCandidate("抱歉，今天一直在忙，没顾上看手机，你说的我都看到了。", "稳妥", "高情商话术"),
                new Core.ReplyCandidate("是我疏忽了，先说说你今天怎么样？", "有个性", "高情商话术")
            }),
            new Core.ReplyGroup("自然接话", new[]
            {
                new Core.ReplyCandidate("在的在的，刚在忙别的，怎么啦？", "稳妥", "自然接话"),
                new Core.ReplyCandidate("哎我刚忙完，这就来报到了。", "有个性", "自然接话")
            }),
            new Core.ReplyGroup("稳如老狗", new[]
            {
                new Core.ReplyCandidate("刚在忙，现在有空了，你说。", "稳妥", "稳如老狗"),
                new Core.ReplyCandidate("忙完了，什么事你说吧。", "有个性", "稳如老狗")
            })
        });
        window.ShowDraft(draft);
        window.Show();
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
