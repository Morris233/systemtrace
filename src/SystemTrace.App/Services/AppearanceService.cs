using System.Windows;
using SystemTrace.Core;

namespace SystemTrace.App.Services;

public interface IAppearanceService
{
    string Language { get; }
    bool IsDark { get; }
    void ToggleLanguage();
    void ToggleTheme();
}

public sealed class AppearanceService : IAppearanceService
{
    private readonly ResourceDictionary resources;
    private ResourceDictionary? strings;
    private ResourceDictionary? theme;

    public string Language { get; private set; }
    public bool IsDark { get; private set; } = true;

    public AppearanceService(ResourceDictionary resources, string? uiCultureName)
    {
        this.resources = resources;
        Language = LanguagePolicy.Resolve(uiCultureName);
        Replace(ref theme, $"Themes/Dark.xaml");
        Replace(ref strings, $"Strings/{Language}.xaml");
    }

    public void ToggleLanguage()
    {
        Language = Language == "en-US" ? "zh-CN" : "en-US";
        Replace(ref strings, $"Strings/{Language}.xaml");
    }

    public void ToggleTheme()
    {
        IsDark = !IsDark;
        Replace(ref theme, $"Themes/{(IsDark ? "Dark" : "Light")}.xaml");
    }

    private void Replace(ref ResourceDictionary? previous, string relativePath)
    {
        var replacement = new ResourceDictionary
        {
            Source = new Uri($"/SystemTrace.App;component/Resources/{relativePath}", UriKind.Relative)
        };
        if (previous is not null)
            resources.MergedDictionaries.Remove(previous);
        resources.MergedDictionaries.Add(replacement);
        previous = replacement;
    }
}
