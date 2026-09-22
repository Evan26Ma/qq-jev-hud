using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using QQJevHud.Services;

namespace QQJevHud.Core;

/// <summary>
/// Minimal OpenAI-compatible chat-completions client (OpenAI / DeepSeek / Ollama / vLLM / …).
/// Shared by the judgment provider and the reply generator so there is exactly one place that
/// talks to the user-configured endpoint. The API key is read from the Credential Manager and
/// never stored or logged here.
/// </summary>
public sealed class OpenAiChat
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly string _baseUrl;
    private readonly string _model;

    public OpenAiChat(string baseUrl, string model)
    {
        _baseUrl = (string.IsNullOrWhiteSpace(baseUrl) ? AppSettings.DefaultBaseUrl : baseUrl).Trim().TrimEnd('/');
        _model = string.IsNullOrWhiteSpace(model) ? AppSettings.DefaultModel : model;
    }

    public bool HasKey => !string.IsNullOrWhiteSpace(CredentialStore.ReadOpenAiKey());

    /// <summary>Sends one system+user exchange and returns the assistant text. Throws on failure.</summary>
    public async Task<string> CompleteAsync(string system, string user, CancellationToken cancellationToken, bool requireJson = true)
    {
        var key = CredentialStore.ReadOpenAiKey();
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("未配置 OpenAI 兼容 API Key");

        var payload = new Dictionary<string, object?>
        {
            ["model"] = _model,
            ["messages"] = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            }
        };
        if (requireJson) payload["response_format"] = new { type = "json_object" };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await Client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"LLM 请求失败：{(int)response.StatusCode}");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return ReadContent(document.RootElement);
    }

    /// <summary>Extracts assistant text from a chat-completions (or legacy completions) response.</summary>
    public static string ReadContent(JsonElement root)
    {
        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var chat) && chat.ValueKind == JsonValueKind.String)
                return chat.GetString() ?? string.Empty;
            if (first.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                return text.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    /// <summary>Strips Markdown fences and returns the outermost JSON object.</summary>
    public static string ExtractJson(string content)
    {
        var text = content.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var newline = text.IndexOf('\n');
            if (newline >= 0) text = text[(newline + 1)..];
            var fence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0) text = text[..fence];
        }
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text.Trim();
    }
}
