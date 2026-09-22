using System.Text;
using System.Text.Json;
using QQJevHud.Services;

namespace QQJevHud.Core;

/// <summary>
/// Decision provider backed by any OpenAI-compatible chat-completions endpoint. It asks for the
/// same dimensions as Jev and maps the reply through the shared <see cref="DecisionCardFactory"/>,
/// so the card looks identical no matter which engine judged it.
/// </summary>
public sealed class OpenAiDecisionProvider : IDecisionProvider
{
    private readonly OpenAiChat _chat;
    private readonly string _profile;

    public OpenAiDecisionProvider(string baseUrl, string model, string? profile = null)
    {
        _chat = new OpenAiChat(baseUrl, model);
        _profile = profile ?? JudgmentProfiles.General;
    }

    private string SystemPrompt =>
        "你是一个沟通意图分析器。针对给定的聊天上下文，判断「当前对方消息」，只输出一个严格 JSON 对象（不要解释、不要 Markdown 围栏）：\n" +
        "{\n" +
        "  \"subtext\":       { \"probabilities\": { \"无明显潜台词\": 0, \"试探在意程度\": 0, \"含蓄表达不满\": 0, \"暗示某个期待\": 0 } },\n" +
        "  \"intent\":        { \"probabilities\": { \"希望具体回应\": 0, \"确认被在意\": 0, \"表达情绪\": 0, \"自然分享\": 0, \"寻求帮助\": 0, \"提出请求\": 0 }, \"confidence\": 0 },\n" +
        "  \"emotion\":       { \"probabilities\": { \"平静\": 0, \"开心\": 0, \"期待\": 0, \"失落\": 0, \"焦虑\": 0, \"生气\": 0, \"冷淡\": 0 } },\n" +
        "  \"tone_distance\": { \"probabilities\": { \"客气疏远\": 0, \"略显生分\": 0, \"正常自然\": 0, \"亲近随意\": 0, \"很亲密\": 0 } },\n" +
        "  \"relationship_state\": { \"probabilities\": { \"正常推进\": 0, \"气氛紧张\": 0, \"正在缓和\": 0, \"僵持\": 0, \"有点疏远\": 0 } },\n" +
        "  \"expectation\":   { \"probabilities\": { \"要具体回答\": 0, \"要情绪安抚\": 0, \"要陪伴聊天\": 0, \"要一个安排\": 0, \"没什么期待\": 0 } },\n" +
        "  \"how_to_reply\":  { \"probabilities\": { \"认真回应重点\": 0, \"先安抚情绪\": 0, \"给具体方案\": 0, \"轻松接话\": 0, \"先问清楚\": 0, \"设置边界\": 0 } },\n" +
        "  \"relationship_risk\": { \"score\": 0 },\n" +
        "  \"needs_response\":    { \"value\": 0 }\n" +
        "}\n" +
        "规则：每项 probabilities 内的数字是各自概率（同一项内和为 1，0~1 小数）；relationship_risk.score 取 0~2（0=低，1=中，2=高）；needs_response.value 取 0~1；intent.confidence 取 0~1。\n" +
        JudgmentProfiles.Scene(_profile) + "\n" +
        "判断方向：" + JudgmentProfiles.RiskFraming(_profile);

    public async Task<DecisionCard> AnalyzeAsync(ChatMessage message, IReadOnlyList<ChatMessage> context, CancellationToken cancellationToken, string? contactNotes = null)
    {
        if (!_chat.HasKey)
            return DecisionCardFactory.Unavailable(message.Id, "LLM", "未配置 OpenAI 兼容 API Key");
        try
        {
            var content = await _chat.CompleteAsync(SystemPrompt, BuildUserContent(message, context, contactNotes), cancellationToken);
            return ParseJudgment(message.Id, content);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return DecisionCardFactory.Failed(message.Id, "LLM", ex.Message);
        }
    }

    private static string BuildUserContent(ChatMessage message, IReadOnlyList<ChatMessage> context, string? contactNotes)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(contactNotes))
        {
            builder.AppendLine("对方背景（你们的约定 / 雷区 / 近况）：");
            builder.AppendLine(contactNotes.Trim());
            builder.AppendLine();
        }
        builder.AppendLine("最近对话（对方/我）：");
        foreach (var item in context.TakeLast(10))
            builder.AppendLine($"{(item.Direction == MessageDirection.Incoming ? "对方" : "我")}：{item.Text}");
        builder.AppendLine();
        builder.Append("当前对方消息：").Append(message.Text);
        return builder.ToString();
    }

    /// <summary>Parses the model's judgment JSON (tolerating code fences and bare/nested numbers).</summary>
    public static DecisionCard ParseJudgment(string messageId, string content)
    {
        try
        {
            using var judge = JsonDocument.Parse(OpenAiChat.ExtractJson(content));
            var root = judge.RootElement;

            IReadOnlyList<DecisionOption> options = Array.Empty<DecisionOption>();
            var confidence = 0;
            if (root.TryGetProperty("intent", out var intent) && intent.ValueKind == JsonValueKind.Object)
            {
                options = DecisionCardFactory.ReadOptions(intent);
                confidence = Num(intent, "confidence") is { } conf ? (int)Math.Round(conf * 100) : 0;
            }

            var riskScore = 1d;
            if (root.TryGetProperty("relationship_risk", out var risk))
                riskScore = Value(risk) ?? Num(risk, "score") ?? 1d;

            var needValue = 0d;
            if (root.TryGetProperty("needs_response", out var need))
                needValue = Value(need) ?? Num(need, "value") ?? 0d;

            var dimensions = DecisionCardFactory.Dimensions(
                ("潜台词", DecisionCardFactory.Dimension(root, "subtext", "潜台词")),
                ("真实意图", DecisionCardFactory.Dimension(root, "intent", "真实意图", highlight: true)),
                ("情绪状态", DecisionCardFactory.Dimension(root, "emotion", "情绪状态")),
                ("怎么回", DecisionCardFactory.Dimension(root, "how_to_reply", "怎么回", highlight: true)),
                ("语气亲疏", DecisionCardFactory.Dimension(root, "tone_distance", "语气亲疏")),
                ("对方期待", DecisionCardFactory.Dimension(root, "expectation", "对方期待")),
                ("关系状态", DecisionCardFactory.Dimension(root, "relationship_state", "关系状态")));

            return DecisionCardFactory.Build(messageId, options, DecisionCardFactory.ToRiskLevel(riskScore), needValue >= .5, confidence, dimensions);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return DecisionCardFactory.Failed(messageId, "LLM", "LLM 返回无法解析");
        }
    }

    private static double? Value(JsonElement element) => element.ValueKind == JsonValueKind.Number ? element.GetDouble() : null;

    private static double? Num(JsonElement parent, string property) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(property, out var value) ? Value(value) : null;
}
