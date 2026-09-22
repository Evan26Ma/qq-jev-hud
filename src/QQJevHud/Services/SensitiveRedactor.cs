using System.Text.RegularExpressions;

namespace QQJevHud.Services;

/// <summary>
/// Best-effort, in-memory masking of sensitive tokens (account / card / ID / phone / password /
/// amounts) before any message text leaves for a decision provider. Nothing is persisted.
/// </summary>
public static class SensitiveRedactor
{
    private static readonly Regex SecretWords = new(
        @"(密码|口令|身份证|证件号|卡号|账号|账户|银行卡|手机号|电话|邮箱)\s*[:：=]?\s*\S+",
        RegexOptions.Compiled);

    private static readonly Regex Amount = new(@"[¥￥$]\s*\d+(?:\.\d+)?", RegexOptions.Compiled);

    private static readonly Regex LongNumber = new(@"\d{11,}", RegexOptions.Compiled);

    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var result = SecretWords.Replace(text, "$1［已脱敏］");
        result = Amount.Replace(result, "［金额］");
        result = LongNumber.Replace(result, "［号码］");
        return result;
    }
}
