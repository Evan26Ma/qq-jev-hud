using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using QQJevHud.Services;

namespace QQJevHud.Core;

/// <summary>
/// Decision provider backed by TypeSafe's Jev (System One) structured-judgment API. It asks the
/// full set of typed questions in one parallel call and maps them through
/// <see cref="DecisionCardFactory"/> into a rich, multi-dimension card.
/// </summary>
public sealed class TypeSafeDecisionProvider : IDecisionProvider
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly string _profile;

    public TypeSafeDecisionProvider(string? profile = null) => _profile = profile ?? JudgmentProfiles.General;

    public async Task<DecisionCard> AnalyzeAsync(ChatMessage message, IReadOnlyList<ChatMessage> context, CancellationToken cancellationToken, string? contactNotes = null)
    {
        var key = CredentialStore.ReadTypeSafeKey();
        if (string.IsNullOrWhiteSpace(key)) return DecisionCardFactory.Unavailable(message.Id, "Jev", "未配置 Jev API Key");
        var recent = context.TakeLast(10).Select(item => new { side = item.Direction == MessageDirection.Incoming ? "对方" : "我", text = item.Text });
        var payload = new
        {
            model = "jev-latest",
            state = new
            {
                scene = JudgmentProfiles.Scene(_profile),
                contact_notes = string.IsNullOrWhiteSpace(contactNotes) ? null : contactNotes.Trim(),
                current_message = message.Text,
                visible_context = recent
            },
            questions = new Dictionary<string, object>
            {
                ["subtext"] = new { type = "choice", instructions = "这句话字面之外，是否有没直说的意思？", criteria = new Dictionary<string, string> { ["无明显潜台词"] = "就是字面意思，没有额外含义", ["试探在意程度"] = "在试探你是否在意、是否记得", ["含蓄表达不满"] = "用委婉方式表达不满或失望", ["暗示某个期待"] = "暗示希望你做某件事" } },
                ["emotion"] = new { type = "choice", instructions = "对方当前的情绪状态是什么？", criteria = new Dictionary<string, string> { ["平静"] = "情绪平稳", ["开心"] = "心情不错、愉快", ["期待"] = "在期待某个回应或事情", ["失落"] = "有些失落或难过", ["焦虑"] = "不安、着急", ["生气"] = "不满或愤怒", ["冷淡"] = "态度冷淡、敷衍" } },
                ["tone_distance"] = new { type = "score", instructions = "这句话的语气有多亲近？", criteria = new[] { "客气疏远", "略显生分", "正常自然", "亲近随意", "很亲密" } },
                ["intent"] = new { type = "choice", instructions = "这条对方消息最主要的沟通意图是什么？", criteria = new Dictionary<string, string> { ["希望具体回应"] = "期待对方回答、解释或完成一个具体动作", ["确认被在意"] = "试探关系、在意程度或记忆", ["表达情绪"] = "主要在表达不满、难过或压力", ["自然分享"] = "主要是在分享近况或轻松聊天", ["寻求帮助"] = "在请求帮助或建议", ["提出请求"] = "在提出一个具体要求" } },
                ["relationship_state"] = new { type = "choice", instructions = "此刻你们的对话关系处于什么状态？", criteria = new Dictionary<string, string> { ["正常推进"] = "交流顺畅，关系正常", ["气氛紧张"] = "有紧张或对抗的气氛", ["正在缓和"] = "气氛在往好的方向缓和", ["僵持"] = "双方僵持、都没让步", ["有点疏远"] = "感觉有些疏远或客套" } },
                ["expectation"] = new { type = "choice", instructions = "对方现在最期待你做什么？", criteria = new Dictionary<string, string> { ["要具体回答"] = "期待一个明确的回答", ["要情绪安抚"] = "期待被安抚、被理解", ["要陪伴聊天"] = "期待陪着聊天、有回应", ["要一个安排"] = "期待定下时间、计划或安排", ["没什么期待"] = "只是说说，没有特别期待" } },
                ["how_to_reply"] = new { type = "choice", instructions = "这条消息最适合怎么回？", criteria = new Dictionary<string, string> { ["认真回应重点"] = "认真、正面地回应核心问题", ["先安抚情绪"] = "先照顾情绪，再谈事情", ["给具体方案"] = "给出具体、可执行的方案", ["轻松接话"] = "轻松地顺着话接下去", ["先问清楚"] = "先问清楚背景再回应", ["设置边界"] = "礼貌但明确地划清边界" } },
                ["relationship_risk"] = new { type = "score", instructions = JudgmentProfiles.RiskFraming(_profile), criteria = new[] { "低：正常交流", "中：需要认真回应", "高：可能升级矛盾" } },
                ["needs_response"] = new { type = "noul", instructions = "对方现在是否期待一个明确、认真而非敷衍的回应？" }
            }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.typesafe.ai/v1/systemone");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await Client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return DecisionCardFactory.Failed(message.Id, "Jev", $"Jev 请求失败：{(int)response.StatusCode}");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var answers = document.RootElement.GetProperty("answers");
        var intent = answers.GetProperty("intent");
        var options = DecisionCardFactory.ReadOptions(intent);
        var riskScore = answers.TryGetProperty("relationship_risk", out var risk) && risk.TryGetProperty("score", out var score) ? score.GetDouble() : 1d;
        var needsResponse = answers.TryGetProperty("needs_response", out var responseNeed) && responseNeed.TryGetProperty("noul", out var noul) && noul.GetDouble() >= .5;
        var confidence = intent.TryGetProperty("confidence", out var confidenceValue) ? (int)Math.Round(confidenceValue.GetDouble() * 100) : 0;
        var dimensions = DecisionCardFactory.Dimensions(
            ("潜台词", DecisionCardFactory.Dimension(answers, "subtext", "潜台词")),
            ("真实意图", DecisionCardFactory.Dimension(answers, "intent", "真实意图", highlight: true)),
            ("情绪状态", DecisionCardFactory.Dimension(answers, "emotion", "情绪状态")),
            ("怎么回", DecisionCardFactory.Dimension(answers, "how_to_reply", "怎么回", highlight: true)),
            ("语气亲疏", DecisionCardFactory.ScoreDimension(answers, "tone_distance", "语气亲疏")),
            ("对方期待", DecisionCardFactory.Dimension(answers, "expectation", "对方期待")),
            ("关系状态", DecisionCardFactory.Dimension(answers, "relationship_state", "关系状态")));
        return DecisionCardFactory.Build(message.Id, options, DecisionCardFactory.ToRiskLevel(riskScore), needsResponse, confidence, dimensions);
    }
}
