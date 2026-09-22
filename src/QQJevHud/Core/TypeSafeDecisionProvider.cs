using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using QQJevHud.Services;

namespace QQJevHud.Core;

public sealed class TypeSafeDecisionProvider : IDecisionProvider
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<DecisionCard> AnalyzeAsync(ChatMessage message, IReadOnlyList<ChatMessage> context, CancellationToken cancellationToken)
    {
        var key = CredentialStore.ReadTypeSafeKey();
        if (string.IsNullOrWhiteSpace(key)) return Unavailable(message.Id, "未配置 Jev API Key");
        var recent = context.TakeLast(10).Select(item => new { side = item.Direction == MessageDirection.Incoming ? "对方" : "我", text = item.Text });
        var payload = new
        {
            model = "jev-latest",
            state = new { current_message = message.Text, visible_context = recent },
            questions = new Dictionary<string, object>
            {
                ["intent"] = new { type = "choice", instructions = "这条对方消息最主要的沟通意图是什么？", criteria = new Dictionary<string, string>
                    { ["希望具体回应"] = "期待对方回答、解释或完成一个具体动作", ["确认被在意"] = "试探关系、在意程度或记忆", ["表达情绪"] = "主要在表达不满、难过或压力", ["自然分享"] = "主要是在分享近况或轻松聊天" } },
                ["relationship_risk"] = new { type = "score", instructions = "这条消息若被敷衍或误解，对当前对话的关系风险有多高？", criteria = new[] { "低：正常交流", "中：需要认真回应", "高：可能升级矛盾" } },
                ["needs_response"] = new { type = "noul", instructions = "对方现在是否期待一个明确、认真而非敷衍的回应？" }
            }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.typesafe.ai/v1/systemone");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await Client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return Failed(message.Id, $"Jev 请求失败：{(int)response.StatusCode}");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var answers = document.RootElement.GetProperty("answers");
        var intent = answers.GetProperty("intent");
        var risk = answers.GetProperty("relationship_risk");
        var responseNeed = answers.GetProperty("needs_response");
        var options = ReadOptions(intent);
        var riskScore = risk.TryGetProperty("score", out var score) ? score.GetDouble() : 1d;
        var riskLevel = Math.Clamp((int)Math.Round(riskScore / 2d * 10d), 1, 10);
        var needsResponse = responseNeed.TryGetProperty("noul", out var noul) && noul.GetDouble() >= .5;
        var confidence = intent.TryGetProperty("confidence", out var confidenceValue) ? (int)Math.Round(confidenceValue.GetDouble() * 100) : 0;
        return new DecisionCard(
            message.Id,
            "对方真实意图",
            "她此刻最希望你怎样回应？",
            options,
            riskLevel,
            riskLevel >= 8 ? "高风险" : riskLevel >= 5 ? "需要留意" : "低风险",
            needsResponse ? "认真回应重点，不要立刻转移话题" : "自然回应并顺着话题继续",
            confidence,
            confidence < 45 ? AnalysisState.LowConfidence : AnalysisState.Ready);
    }

    private static IReadOnlyList<DecisionOption> ReadOptions(JsonElement answer)
    {
        if (!answer.TryGetProperty("probabilities", out var probabilities) || probabilities.ValueKind != JsonValueKind.Object) return Array.Empty<DecisionOption>();
        return probabilities.EnumerateObject()
            .Select(property => new DecisionOption(property.Name, (int)Math.Round(property.Value.GetDouble() * 100)))
            .OrderByDescending(option => option.Probability)
            .Take(4)
            .ToArray();
    }

    private static DecisionCard Unavailable(string id, string reason) => new(id, "Jev 未配置", reason, Array.Empty<DecisionOption>(), 0, "未启用", "无需操作", 0, AnalysisState.Unavailable);
    private static DecisionCard Failed(string id, string reason) => new(id, "Jev 请求失败", reason, Array.Empty<DecisionOption>(), 0, "失败", "请稍后重试", 0, AnalysisState.Failed);
}
