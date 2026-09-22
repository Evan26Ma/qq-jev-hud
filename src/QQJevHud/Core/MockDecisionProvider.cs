using System.Security.Cryptography;
using System.Text;

namespace QQJevHud.Core;

/// <summary>
/// Offline, deterministic provider for demos and development (no key, no network). It produces the
/// same rich dimension set as the real engines so the card layout can be evaluated without keys.
/// </summary>
public sealed class MockDecisionProvider : IDecisionProvider
{
    public Task<DecisionCard> AnalyzeAsync(
        ChatMessage message,
        IReadOnlyList<ChatMessage> context,
        CancellationToken cancellationToken,
        string? contactNotes = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var seed = SHA256.HashData(Encoding.UTF8.GetBytes(message.Id));
        var isQuestion = message.Text.Contains('？') || message.Text.Contains('?');
        var isEmotional = message.Text.Contains('烦') || message.Text.Contains('气') || message.Text.Contains('累') || message.Text.Contains("难过", StringComparison.Ordinal);
        var topic = isQuestion ? "她在确认什么？" : isEmotional ? "情绪需要被接住吗？" : "这句话更在意什么？";
        var question = isQuestion ? "她是否期待你认真回应？" : isEmotional ? "此刻是否需要先安抚情绪？" : "她是在表达需求还是分享近况？";
        var labels = isQuestion
            ? new[] { "希望你给出具体回答", "想确认你是否在意", "只是随口一问" }
            : isEmotional
                ? new[] { "需要被理解", "希望你主动关心", "只是表达一下" }
                : new[] { "希望得到回应", "在分享近况", "想推进一个安排" };
        var first = 48 + seed[0] % 28;
        var second = 12 + seed[1] % 20;
        var third = 100 - first - second;
        var risk = 1 + seed[2] % 10;
        var action = risk >= 8 ? "先确认感受，再讨论事实" : risk >= 5 ? "认真回应重点，不要立刻转移话题" : "自然回应并顺着话题继续";
        var confidence = 62 + seed[3] % 28;

        var card = new DecisionCard(
            message.Id,
            topic,
            question,
            new[] { new DecisionOption(labels[0], first), new DecisionOption(labels[1], second), new DecisionOption(labels[2], third) },
            risk,
            risk >= 8 ? "高风险" : risk >= 5 ? "需要留意" : "低风险",
            action,
            confidence,
            AnalysisState.Ready)
        {
            Dimensions = BuildDimensions(seed, labels[0], isEmotional)
        };
        return Task.FromResult(card);
    }

    private static IReadOnlyList<JudgmentDimension> BuildDimensions(byte[] seed, string intentTop, bool isEmotional) =>
        DecisionCardFactory.Dimensions(
            ("潜台词", Option(seed, 4, "潜台词", "无明显潜台词", "试探在意程度", "含蓄表达不满", "暗示某个期待")),
            ("真实意图", new JudgmentDimension("真实意图",
                new[] { new DecisionOption(intentTop, 48 + seed[4] % 24), new DecisionOption("其他意图", 12 + seed[5] % 16), new DecisionOption("不清楚", 6 + seed[6] % 10) },
                Highlight: true)),
            ("情绪状态", Option(seed, 7, "情绪状态", isEmotional ? "烦躁" : "平静", "开心", "失落", "焦虑", "生气", "冷淡")),
            ("怎么回", Highlight(Option(seed, 8, "怎么回", "认真回应重点", "先安抚情绪", "轻松接话", "先问清楚"))),
            ("语气亲疏", Option(seed, 9, "语气亲疏", "正常自然", "亲近随意", "略显生分", "客气疏远")),
            ("对方期待", Option(seed, 10, "对方期待", "要具体回答", "要情绪安抚", "要陪伴聊天", "没什么期待")),
            ("关系状态", Option(seed, 11, "关系状态", "正常推进", "气氛紧张", "正在缓和", "有点疏远")));

    /// <summary>Marks a dimension as one of the two headline judgments (★ in the card).</summary>
    private static JudgmentDimension Highlight(JudgmentDimension dimension) => dimension with { Highlight = true };

    /// <summary>Deterministic pseudo-distribution over the given labels (sums to 100).</summary>
    private static JudgmentDimension Option(byte[] seed, int offset, string label, params string[] labels)
    {
        var weights = new int[labels.Length];
        var total = 0;
        for (var i = 0; i < labels.Length; i++)
        {
            weights[i] = 10 + seed[(offset + i) % seed.Length] % 70;
            total += weights[i];
        }
        var options = new List<DecisionOption>();
        var assigned = 0;
        for (var i = 0; i < labels.Length; i++)
        {
            var share = i == labels.Length - 1 ? 100 - assigned : weights[i] * 100 / total;
            assigned += share;
            options.Add(new DecisionOption(labels[i], share));
        }
        return new JudgmentDimension(label, options.OrderByDescending(option => option.Probability).ToArray());
    }
}
