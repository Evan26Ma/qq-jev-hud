namespace QQJevHud.Core;

/// <summary>
/// Builds the configured decision provider. Selection lives in settings; <c>QQJEVHUD_MOCK=1</c>
/// remains the highest-priority override for offline demos and consent-safe development.
/// </summary>
public static class DecisionProviderFactory
{
    public static IDecisionProvider Create(AppSettings settings)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("QQJEVHUD_MOCK"), "1", StringComparison.Ordinal))
            return new MockDecisionProvider();

        return (settings.Provider ?? AppSettings.DefaultProvider).Trim().ToLowerInvariant() switch
        {
            "openai" => new OpenAiDecisionProvider(settings.BaseUrl, settings.Model, settings.Profile),
            "mock" => new MockDecisionProvider(),
            _ => new TypeSafeDecisionProvider(settings.Profile),
        };
    }
}
