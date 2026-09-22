using QQJevHud.Core;

namespace QQJevHud.Views;

/// <summary>
/// Sample content for the "先看看效果" buttons: renders a judgment card and a choice panel from local
/// mock data, so the UI can be inspected without QQ, a key, or any network call.
/// </summary>
public static class Preview
{
    private const string SampleMessage = "我昨天买的M7五级弹，4400一发，怕是有点难赚";

    public static void ShowCardPreview(Action? openSettings = null)
    {
        var (message, card) = Sample();
        var overlay = new OverlayWindow();
        var area = new ScreenRect(60, 60, 940, 720);
        var layouts = new OverlayLayoutEngine().Arrange(new[] { (message, card) }, area);
        overlay.ShowLayouts(area, 96, layouts, showOptions: true, showRisk: true, showAdvice: true);
        // Keep the sample on screen briefly; it is an overlay, so close it on a timer.
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        timer.Tick += (_, _) => { overlay.Close(); timer.Stop(); };
        timer.Start();
    }

    public static void ShowChoicePreview(Action? openSettings = null)
    {
        var (message, card) = Sample();
        // The sample judgment is low-risk, so the choices' predictions line up with it.
        card = card with { RiskLevel = 2, RiskLabel = "低风险", RecommendedAction = "轻松接话，别把话题聊死" };
        var choices = new[]
        {
            new ChoiceItem(1, "确实贵，你是想囤还是自己用？", "理科直男",
                new CandidateOutcome("满意、顺利接住", 58, 2, "低风险")),
            new ChoiceItem(2, "哈哈那你别买，省下的钱请我吃饭。", "阴阳怪气",
                new CandidateOutcome("被安抚、情绪缓和", 62, 2, "低风险")),
            new ChoiceItem(3, "4400 一发也太狠了吧，心疼你三秒。", "高情商话术",
                new CandidateOutcome("被安抚、情绪缓和", 55, 2, "低风险")),
            new ChoiceItem(4, "那你还买，图啥呢？", "自然接话",
                new CandidateOutcome("觉得被敷衍", 41, 5, "需要留意"))
        };
        var set = new ChoiceSet(message.Id, message.Text, message.Sender, card, choices);
        var window = new ChoiceWindow();
        window.ShowChoiceSet(set);
    }

    private static (ChatMessage Message, DecisionCard Card) Sample()
    {
        var message = new ChatMessage("preview", "preview", MessageDirection.Incoming,
            SampleMessage, 1, new ScreenRect(150, 240, 320, 56), "零");
        var card = new MockDecisionProvider()
            .AnalyzeAsync(message, Array.Empty<ChatMessage>(), CancellationToken.None)
            .GetAwaiter().GetResult();
        return (message, card);
    }
}
