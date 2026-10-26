namespace SystemTrace.Core;

public static class LanguagePolicy
{
    public static string Resolve(string? uiCultureName) =>
        uiCultureName?.Split('-')[0].Equals("zh", StringComparison.OrdinalIgnoreCase) == true
            ? "zh-CN"
            : "en-US";
}
