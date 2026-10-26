using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SystemTrace.App.Services;
using SystemTrace.Core;

namespace SystemTrace.App.ViewModels;

public sealed record OverviewPageViewModel(DemoOverview Overview);
public sealed record TimelinePageViewModel;
public sealed record SnapshotsPageViewModel;
public sealed record TroubleshootPageViewModel;
public sealed record SettingsPageViewModel;

public partial class MainViewModel : ObservableObject
{
    private readonly IAppearanceService appearance;
    private readonly ILogger<MainViewModel> logger;
    public IReadOnlyList<object> Pages { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage))]
    private int selectedPageIndex;

    public object CurrentPage => Pages.ElementAtOrDefault(SelectedPageIndex) ?? Pages[0];

    public MainViewModel(IDemoDataProvider demo, IAppearanceService appearance, ILogger<MainViewModel> logger)
    {
        this.appearance = appearance;
        this.logger = logger;
        Pages = Array.AsReadOnly<object>([
            new OverviewPageViewModel(demo.GetOverview()),
            new TimelinePageViewModel(),
            new SnapshotsPageViewModel(),
            new TroubleshootPageViewModel(),
            new SettingsPageViewModel()
        ]);
        logger.LogInformation("SystemTrace PHASE 01 demo shell initialized");
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        appearance.ToggleLanguage();
        logger.LogInformation("Display language changed to {Language}", appearance.Language);
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        appearance.ToggleTheme();
        logger.LogInformation("Dark theme: {DarkTheme}", appearance.IsDark);
    }
}
