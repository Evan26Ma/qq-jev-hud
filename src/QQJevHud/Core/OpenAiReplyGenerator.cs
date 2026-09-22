using System.Text;
using System.Text.Json;

namespace QQJevHud.Core;

/// <summary>
/// "话我帮你想" — generates candidate replies for one incoming message, grouped by tone
/// ("话术"), through the OpenAI-compatible endpoint. Two candidates per tone: one 稳妥 (safe)
/// and one 有个性 (on-character). Sending is always left to the user.
/// </summary>
public sealed class OpenAiReplyGenerator
{
    private readonly OpenAiChat _chat;

    public OpenAiReplyGenerator(string baseUrl, string model) => _chat = new OpenAiChat(baseUrl, model);

    public bool HasKey => _chat.HasKey;

    /// <summary>Generates candidates for every tone in one request (cheaper and faster than one call per tone).</summary>
    public async Task<ReplyDraft> GenerateAsync(
        ChatMessage message,
        IReadOnlyList<ChatMessage> context,
        IReadOnlyList<Tone> tones,
        string? contactNotes,
        CancellationToken cancellationToken)
    {
        if (tones.Count == 0 || !_chat.HasKey)
            return new ReplyDraft(message.Id, message.Text, Array.Empty<ReplyGroup>());

        var system = BuildSystemPrompt(tones);
        var user = BuildUserContent(message, context, contactNotes);
        var content = await _chat.CompleteAsync(system, user, cancellationToken);
        return ParseDraft(message.Id, message.Text, content, tones);
    }

    private static string BuildSystemPrompt(IReadOnlyList<Tone> tones)
    {
        var builder = new StringBuilder();
        builder.AppendLine("你是 QQ 聊天助手，帮用户想「可以怎么回」。根据给定的聊天上下文和「当前对方消息」，为下面每种话术风格各写 2 条可以直接发送的候选回复：");
        builder.AppendLine("- 一条「稳妥」：得体、安全、不出错；一条「有个性」：更贴合该风格、生动。");
        builder.AppendLine("要求：简短自然（像真实 QQ 聊天，通常一句话），符合该话术语气；不要解释、不要括号说明、不要 emoji 占位、不要把回复用引号包起来。发送与否由用户决定，你只负责拟稿。");
        builder.AppendLine("只输出严格 JSON（不要 Markdown 围栏）：{\"groups\":[{\"tone\":\"<话术名>\",\"replies\":[{\"style\":\"稳妥\",\"text\":\"...\"},{\"style\":\"有个性\",\"text\":\"...\"}]}]}");
        builder.AppendLine("话术：");
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

    /// <summary>Parses the model's JSON into a draft, tolerating fences and stray whitespace.</summary>
    public static ReplyDraft ParseDraft(string messageId, string messageText, string content, IReadOnlyList<Tone> tones)
    {
        var groups = new List<ReplyGroup>();
        try
        {
            using var document = JsonDocument.Parse(OpenAiChat.ExtractJson(content));
            if (document.RootElement.TryGetProperty("groups", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var group in array.EnumerateArray())
                {
                    var tone = group.TryGetProperty("tone", out var toneValue) ? toneValue.GetString() ?? string.Empty : string.Empty;
                    var candidates = new List<ReplyCandidate>();
                    if (group.TryGetProperty("replies", out var replies) && replies.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var reply in replies.EnumerateArray())
                        {
                            var style = reply.TryGetProperty("style", out var styleValue) ? styleValue.GetString() ?? string.Empty : string.Empty;
                            var text = reply.TryGetProperty("text", out var textValue) ? textValue.GetString() ?? string.Empty : string.Empty;
                            if (!string.IsNullOrWhiteSpace(text))
                                candidates.Add(new ReplyCandidate(text.Trim(), style, string.IsNullOrWhiteSpace(tone) ? GuessTone(text, tones) : tone));
                        }
                    }
                    if (candidates.Count > 0) groups.Add(new ReplyGroup(tone, candidates));
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            // fall through to an empty draft rather than breaking the HUD
        }
        return new ReplyDraft(messageId, messageText, groups);
    }

    private static string GuessTone(string text, IReadOnlyList<Tone> tones) => tones.Count > 0 ? tones[0].Name : "回复";
}
