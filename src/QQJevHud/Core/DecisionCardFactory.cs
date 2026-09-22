using System.Text.Json;

namespace QQJevHud.Core;

/// <summary>
/// Shared mapping from judgment primitives to the rendered <see cref="DecisionCard"/>. Every
/// decision provider (Jev, OpenAI-compatible LLM, mock) funnels through here so the cards look and
/// scale identically regardless of which engine produced the judgment.
/// </summary>
public static class DecisionCardFactory
{
    public const string Topic = "对方真实意图";
    public const string Question = "她此刻最希望你怎样回应？";

    /// <summary>Probabilities object (label → 0..1) becomes ranked options, top 4.</summary>
    public static IReadOnlyList<DecisionOption> ReadOptions(JsonElement answer)
    {
        if (answer.ValueKind != JsonValueKind.Object) return Array.Empty<DecisionOption>();
        if (!answer.TryGetProperty("probabilities", out var probabilities) || probabilities.ValueKind != JsonValueKind.Object)
            return Array.Empty<DecisionOption>();
        return probabilities.EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.Number)
            .Select(property => new DecisionOption(property.Name, (int)Math.Round(property.Value.GetDouble() * 100)))
            .OrderByDescending(option => option.Probability)
            .Take(4)
            .ToArray();
    }

    /// <summary>relationship_risk score is 0..2 (low/mid/high); map it onto the 1..10 card scale.</summary>
    public static int ToRiskLevel(double score) => Math.Clamp((int)Math.Round(score / 2d * 10d), 1, 10);

    public static string RiskLabel(int riskLevel) => riskLevel >= 8 ? "高风险" : riskLevel >= 5 ? "需要留意" : "低风险";

    public static string RecommendedAction(bool needsResponse) =>
        needsResponse ? "认真回应重点，不要立刻转移话题" : "自然回应并顺着话题继续";

    public static AnalysisState ToState(int confidence) => confidence < 45 ? AnalysisState.LowConfidence : AnalysisState.Ready;

    public static DecisionCard Build(string messageId, IReadOnlyList<DecisionOption> options, int riskLevel, bool needsResponse, int confidence,
        IReadOnlyList<JudgmentDimension>? dimensions = null) =>
        new(messageId, Topic, Question, options, riskLevel, RiskLabel(riskLevel),
            RecommendedAction(needsResponse), confidence, ToState(confidence))
        {
            Dimensions = dimensions ?? Array.Empty<JudgmentDimension>()
        };

    /// <summary>Reads an answer object (with a probabilities map) into a labeled dimension.</summary>
    public static JudgmentDimension? Dimension(JsonElement root, string property, string label, bool highlight = false)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(property, out var answer)) return null;
        // Jev returns {"type":"choice","probabilities":{...}}; the LLM path may return the map directly.
        var options = ReadOptions(answer);
        if (options.Count == 0 && answer.ValueKind == JsonValueKind.Object) options = ReadOptions(root);
        return options.Count == 0 ? null : new JudgmentDimension(label, options, highlight);
    }

    /// <summary>Reads a Jev score answer, naming the levels from its legend (falling back to bare keys).</summary>
    public static JudgmentDimension? ScoreDimension(JsonElement root, string property, string label, bool highlight = false)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(property, out var answer)) return null;
        if (answer.ValueKind != JsonValueKind.Object) return Dimension(root, property, label, highlight);
        if (!answer.TryGetProperty("probabilities", out var probabilities) || probabilities.ValueKind != JsonValueKind.Object) return null;

        var legend = new Dictionary<string, string>(StringComparer.Ordinal);
        if (answer.TryGetProperty("legend", out var legendElement) && legendElement.ValueKind == JsonValueKind.Object)
            foreach (var entry in legendElement.EnumerateObject())
                if (entry.Value.ValueKind == JsonValueKind.String) legend[entry.Name] = entry.Value.GetString() ?? entry.Name;

        var options = probabilities.EnumerateObject()
            .Where(entry => entry.Value.ValueKind == JsonValueKind.Number)
            .Select(entry => new DecisionOption(legend.TryGetValue(entry.Name, out var name) ? name : entry.Name,
                (int)Math.Round(entry.Value.GetDouble() * 100)))
            .OrderByDescending(option => option.Probability)
            .Take(4)
            .ToArray();
        return options.Length == 0 ? null : new JudgmentDimension(label, options, highlight);
    }

    /// <summary>Builds the ordered dimension list, dropping any dimension the model did not answer.</summary>
    public static IReadOnlyList<JudgmentDimension> Dimensions(params (string Label, JudgmentDimension? Dimension)[] candidates) =>
        candidates.Where(candidate => candidate.Dimension is not null).Select(candidate => candidate.Dimension!).ToArray();

    public static DecisionCard Unavailable(string messageId, string service, string reason) =>
        new(messageId, $"{service} 未配置", reason, Array.Empty<DecisionOption>(), 0, "未启用", "无需操作", 0, AnalysisState.Unavailable);

    public static DecisionCard Failed(string messageId, string service, string reason) =>
        new(messageId, $"{service} 请求失败", reason, Array.Empty<DecisionOption>(), 0, "失败", "请稍后重试", 0, AnalysisState.Failed);
}
