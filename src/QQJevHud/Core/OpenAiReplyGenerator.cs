using System.Text;
using System.Text.Json;

namespace QQJevHud.Core;

/// <summary>
/// "话我帮你想" — drafts a short set of candidate replies for one incoming message through the
/// OpenAI-compatible endpoint, using the user's tones as the writing brief. The set is capped at
/// <see cref="MaxChoices"/> because the panel offers a handful of distinct options to pick from, not a
/// wall of variations.
/// </summary>
public sealed class OpenAiReplyGenerator
{
    public const int MaxChoices = 4;

    private readonly OpenAiChat _chat;

    public OpenAiReplyGenerator(string baseUrl, string model) => _chat = new OpenAiChat(baseUrl, model);

    public bool HasKey => _chat.HasKey;

    /// <summary>Generates up to <see cref="MaxChoices"/> distinct candidates in one request.</summary>
    public async Task<IReadOnlyList<ReplyCandidate>> GenerateAsync(
        ChatMessage message,
        IReadOnlyList<ChatMessage> context,
        IReadOnlyList<Tone> tones,
        string? contactNotes,
        CancellationToken cancellationToken)
    {
        if (tones.Count == 0 || !_chat.HasKey) return Array.Empty<ReplyCandidate>();

        var content = await _chat.CompleteAsync(BuildSystemPrompt(tones), BuildUserContent(message, context, contactNotes), cancellationToken);
        var candidates = ParseCandidates(content, tones);
        return candidates.Count > MaxChoices ? candidates.Take(MaxChoices).ToArray() : candidates;
    }

    private static string BuildSystemPrompt(IReadOnlyList<Tone> tones)
    {
        var count = Math.Min(MaxChoices, Math.Max(2, tones.Count));
        var builder = new StringBuilder();
        builder.AppendLine($"你是 QQ 聊天助手，帮用户想「可以怎么回」。根据给定的聊天上下文和「当前对方消息」，写 {count} 条**各不相同的**候选回复。");
        builder.AppendLine("要求：");
        builder.AppendLine($"- 每条都是一个不同的话术角度（从下面的话术里挑），不要同一风格的两种说法。");
        builder.AppendLine("- 简短自然，像真实 QQ 聊天（通常一到两句），可以直接发送。");
        builder.AppendLine("- 不要解释、不要括号说明、不要 emoji 占位、不要把回复用引号包起来。");
        builder.AppendLine("- 发送与否由用户决定，你只负责拟稿。");
        builder.AppendLine("只输出严格 JSON（不要 Markdown 围栏）：{\"choices\":[{\"tone\":\"<话术名>\",\"text\":\"...\"}]}");
        builder.AppendLine("可选话术：");
        foreach (var tone in tones) builder.AppendLine($"- {tone.Name}：{tone.Description}");
        return builder.ToString();
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
        foreach (var item in context.TakeLast(6))
            builder.AppendLine($"{(item.Direction == MessageDirection.Incoming ? "对方" : "我")}：{item.Text}");
        builder.AppendLine();
        builder.Append("当前对方消息：").Append(message.Text);
        return builder.ToString();
    }

    /// <summary>Parses the model's JSON into candidates, tolerating fences and stray formatting.</summary>
    public static IReadOnlyList<ReplyCandidate> ParseCandidates(string content, IReadOnlyList<Tone> tones)
    {
        var candidates = new List<ReplyCandidate>();
        try
        {
            using var document = JsonDocument.Parse(OpenAiChat.ExtractJson(content));
            if (document.RootElement.TryGetProperty("choices", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var choice in array.EnumerateArray())
                {
                    var text = choice.TryGetProperty("text", out var textValue) ? textValue.GetString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    var tone = choice.TryGetProperty("tone", out var toneValue) ? toneValue.GetString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrWhiteSpace(tone)) tone = tones.Count > 0 ? tones[0].Name : "回复";
                    candidates.Add(new ReplyCandidate(text.Trim(), tone));
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            // fall through to an empty set rather than breaking the HUD
        }
        return candidates;
    }
}
