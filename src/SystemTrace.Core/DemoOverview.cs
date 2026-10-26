namespace SystemTrace.Core;

// Phase 01 display contracts only. These are not collected system inventory.
public sealed record DemoCategory(string ResourceKey, int ExampleCount);

public sealed record DemoOverview(bool IsDemo, IReadOnlyList<DemoCategory> Categories);

public interface IDemoDataProvider
{
    DemoOverview GetOverview();
}

public static class LanguagePolicy
{
    public static string Resolve(string? uiCultureName) =>
        uiCultureName?.Split('-')[0].Equals("zh", StringComparison.OrdinalIgnoreCase) == true
            ? "zh-CN"
            : "en-US";
}
