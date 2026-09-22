using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using QQJevHud.Services;

namespace QQJevHud.Core;

/// <summary>
/// Asks Jev what each candidate reply would lead to — the "可能的结果" shown under every choice, like a
/// visual novel previewing the consequence of an option. All candidates are evaluated in ONE request
/// (Jev answers every question in parallel), so a full set costs about as much as a single judgment.
/// </summary>
public sealed class CandidateEvaluator
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Possible reactions, safest first. Shared across candidates so results compare directly.</summary>
    private static readonly Dictionary<string, string> Reactions = new()
    {
        ["被安抚、情绪缓和"] = "对方感到被理解，情绪明显变好",
        ["满意、顺利接住"] = "对方接受了这个回复，对话顺畅推进",
        ["无明显变化"] = "对方反应平淡，话题继续往下走",
        ["觉得被敷衍"] = "对方觉得回答不够认真、在应付",
        ["可能更不满"] = "对方可能因此更不高兴，甚至升级"
    };

    private static readonly string[] RiskCriteria = { "低：正常交流", "中：需要留意", "高：可能升级矛盾" };

    /// <summary>
    /// Evaluates up to <paramref name="candidates"/> replies. Returns one entry per candidate; a null
    /// entry means Jev could not judge that one (the choice is still shown, just without a prediction).
    /// </summary>
    public async Task<IReadOnlyList<CandidateOutcome?>> EvaluateAsync(
        ChatMessage message,
        IReadOnlyList<ChatMessage> context,
        IReadOnlyList<string> candidates,
        string? contactNotes,
        CancellationToken cancellationToken)
    {
        var empty = candidates.Select(_ => (CandidateOutcome?)null).ToArray();
        if (candidates.Count == 0) return empty;
        var key = CredentialStore.ReadTypeSafeKey();
        if (string.IsNullOrWhiteSpace(key)) return empty;

        var questions = new Dictionary<string, object>();
        var replies = new Dictionary<string, string>();
        for (var index = 0; index < candidates.Count; index++)
        {
            replies[$"回复{index + 1}"] = candidates[index];
            questions[$"reaction_{index}"] = new
            {
                type = "choice",
                instructions = $"如果用户用「回复{index + 1}」这样回复，对方最可能的反应是什么？",
                criteria = Reactions
            };
            questions[$"risk_{index}"] = new
            {
                type = "score",
                instructions = $"用户发出「回复{index + 1}」之后，当前对话的关系风险有多高？",
                criteria = RiskCriteria
            };
        }

        var recent = context.TakeLast(8).Select(item => new { side = item.Direction == MessageDirection.Incoming ? "对方" : "我", text = item.Text });
        var payload = new
        {
            model = "jev-latest",
            state = new
            {
                scene = "用户正在决定如何回复对方的这条消息。请评估每个候选回复会带来什么结果。",
                contact_notes = string.IsNullOrWhiteSpace(contactNotes) ? null : contactNotes.Trim(),
                their_message = message.Text,
                visible_context = recent,
                my_possible_replies = replies
            },
            questions
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.typesafe.ai/v1/systemone");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await Client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return empty;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("answers", out var answers)) return empty;

        var results = new List<CandidateOutcome?>(candidates.Count);
        for (var index = 0; index < candidates.Count; index++) results.Add(ReadOutcome(answers, index));
        return results;
    }

    private static CandidateOutcome? ReadOutcome(JsonElement answers, int index)
    {
        if (!answers.TryGetProperty($"reaction_{index}", out var reaction) ||
            !answers.TryGetProperty($"risk_{index}", out var risk)) return null;

        var (label, probability) = TopReaction(reaction);
        if (string.IsNullOrEmpty(label)) return null;

        var score = risk.TryGetProperty("score", out var scoreValue) ? scoreValue.GetDouble() : 1d;
        var riskLevel = DecisionCardFactory.ToRiskLevel(score);
        return new CandidateOutcome(label, probability, riskLevel, DecisionCardFactory.RiskLabel(riskLevel));
    }

    /// <summary>Top reaction label plus its probability, from Jev's choice answer.</summary>
    private static (string Label, int Probability) TopReaction(JsonElement answer)
    {
        if (answer.ValueKind != JsonValueKind.Object) return (string.Empty, 0);
        if (answer.TryGetProperty("probabilities", out var probabilities) && probabilities.ValueKind == JsonValueKind.Object)
        {
            var best = probabilities.EnumerateObject()
                .Where(entry => entry.Value.ValueKind == JsonValueKind.Number)
                .OrderByDescending(entry => entry.Value.GetDouble())
                .FirstOrDefault();
            if (best.Name is { Length: > 0 }) return (best.Name, (int)Math.Round(best.Value.GetDouble() * 100));
        }
        // Fall back to the bare `choice` field if probabilities were not returned.
        if (answer.TryGetProperty("choice", out var choice) && choice.ValueKind == JsonValueKind.String)
            return (choice.GetString() ?? string.Empty, 0);
        return (string.Empty, 0);
    }
}
