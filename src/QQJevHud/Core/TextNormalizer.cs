using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace QQJevHud.Core;

public static partial class TextNormalizer
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^\d{1,2}:\d{2}$")]
    private static partial Regex TimeOnly();

    public static string Normalize(string value) => Whitespace().Replace(value.Trim(), " ");

    public static bool IsMetadata(string value) => TimeOnly().IsMatch(value) || value is "昨天" or "今天";

    public static string Fingerprint(string sessionId, MessageDirection direction, string normalizedText)
    {
        var payload = Encoding.UTF8.GetBytes($"{sessionId}|{direction}|{normalizedText}");
        return Convert.ToHexString(SHA256.HashData(payload))[..16];
    }
}
