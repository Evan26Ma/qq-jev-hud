namespace QQJevHud.Core;

/// <summary>
/// The judgment profile: the same engine judges a message differently in a close relationship than
/// in everyday work groups, so the prompt/questions shift with the mode the user picked.
/// </summary>
public static class JudgmentProfiles
{
    public const string General = "general";
    public const string Relationship = "relationship";

    public static bool IsRelationship(string? profile) =>
        string.Equals(profile, Relationship, StringComparison.OrdinalIgnoreCase);

    public static string Scene(string? profile) => IsRelationship(profile)
        ? "当前场景：亲密关系（情侣 / 夫妻）。判断时更关注情感连接、被在意程度和关系温度。"
        : "当前场景：日常沟通（朋友 / 同事 / 群聊）。判断时更关注事情本身、信息传达和协作关系。";

    public static string RiskFraming(string? profile) => IsRelationship(profile)
        ? "这条消息若被敷衍或误解，对你们关系的伤害有多大？"
        : "这条消息若被敷衍或误解，对当前对话和协作关系的风险有多高？";
}
