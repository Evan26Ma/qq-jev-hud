using System.Security.Cryptography;
using System.Text;

namespace QQJevHud.Core;

public sealed class MockDecisionProvider : IDecisionProvider
{
    public Task<DecisionCard> AnalyzeAsync(
        ChatMessage message,
        IReadOnlyList<ChatMessage> context,
        CancellationToken cancellationToken)
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

        var card = new DecisionCard(
            message.Id,
            topic,
            question,
            new[] { new DecisionOption(labels[0], first), new DecisionOption(labels[1], second), new DecisionOption(labels[2], third) },
            risk,
            risk >= 8 ? "高风险" : risk >= 5 ? "需要留意" : "低风险",
            action,
            62 + seed[3] % 28,
            AnalysisState.Ready);
        return Task.FromResult(card);
    }
}
