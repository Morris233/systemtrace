using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SystemTrace.App.Services;
using SystemTrace.Core;

namespace SystemTrace.App.ViewModels;

public sealed record OverviewPageViewModel(DemoOverview Overview, string StatusText, string DetailText);
public sealed record ChangeRow(string Label, string KindText, string ValueText, string ObservationWindow);
public sealed record TimelinePageViewModel(IReadOnlyList<ChangeRow> Rows);
public sealed record SnapshotRow(string CategoryText, string ItemCount, string ObservedAt);
public sealed record SnapshotsPageViewModel(IReadOnlyList<SnapshotRow> Rows);
public sealed record TroubleshootPageViewModel;
public sealed record SettingsPageViewModel;

public partial class MainViewModel : ObservableObject
{
    private readonly IDemoSession session;
    private readonly IAppearanceService appearance;
    private readonly ILogger<MainViewModel> logger;
    private readonly List<object> pages = [];
    private readonly CancellationTokenSource lifetime = new();
    public IReadOnlyList<object> Pages => pages.AsReadOnly();
    public InventoryHistory History { get; private set; } = InventoryHistory.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage))]
    private int selectedPageIndex;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunDemoCommand))]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    public object CurrentPage => pages.ElementAtOrDefault(SelectedPageIndex) ?? pages[0];

    public MainViewModel(IDemoSession session, IAppearanceService appearance, ILogger<MainViewModel> logger)
    {
        this.session = session;
        this.appearance = appearance;
        this.logger = logger;
        RefreshPages();
    }

    public Task InitializeAsync() => ExecuteAsync(session.LoadAsync, lifetime.Token);
    public void CancelPending()
    {
        lifetime.Cancel();
        RunDemoCommand.Cancel();
    }

    private bool CanRunDemo() => !IsLoading && !lifetime.IsCancellationRequested;

    [RelayCommand(CanExecute = nameof(CanRunDemo), IncludeCancelCommand = true)]
    private async Task RunDemoAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        await ExecuteAsync(session.RunAsync, linked.Token);
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task<InventoryHistory>> action, CancellationToken token)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var history = await action(token);
            if (!lifetime.IsCancellationRequested)
            {
                History = history;
                RefreshPages();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "Demo history operation failed");
            ErrorMessage = appearance.Text("StorageError");
        }
        finally { IsLoading = false; }
    }

    private void RefreshPages()
    {
        pages.Clear();
        var overview = new DemoOverview(true, Array.AsReadOnly(Enum.GetValues<InventoryCategory>()
            .Select(category => new DemoCategory(category.ToString(), History.ChangeCounts.GetValueOrDefault(category))).ToArray()));
        var hasBaseline = History.Baselines.Count > 0;
        var detail = History.LastAttempt is { } last
            ? appearance.Text("LastDemo") + " " + last.CompletedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)
            : appearance.Text("RunDemoHint");
        pages.Add(new OverviewPageViewModel(overview, appearance.Text(hasBaseline ? "BaselineReady" : "BaselineTitle"), detail));
        pages.Add(new TimelinePageViewModel(History.Changes.Select(change =>
        {
            var item = change.After ?? change.Before!;
            var label = appearance.Text(item.Fields.GetValueOrDefault("label_key")?.Value ?? item.StableKey);
            var oldValue = change.Before?.Fields.GetValueOrDefault("value");
            var newValue = change.After?.Fields.GetValueOrDefault("value");
            string Value(FieldValue? field) => field is null ? "—" : field.Value is null ? "∅" : appearance.Text(field.Value);
            return new ChangeRow(label, appearance.Text(change.Kind.ToString()), $"{Value(oldValue)} → {Value(newValue)}",
                $"{change.ObservedFromUtc.ToLocalTime():MM-dd HH:mm:ss} → {change.ObservedToUtc.ToLocalTime():MM-dd HH:mm:ss}");
        }).ToArray()));
        pages.Add(new SnapshotsPageViewModel(History.Baselines.Select(snapshot => new SnapshotRow(
            appearance.Text(snapshot.Category.ToString()), string.Format(CultureInfo.CurrentCulture, appearance.Text("ItemCountFormat"), snapshot.Items.Count),
            snapshot.ObservedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture))).ToArray()));
        pages.Add(new TroubleshootPageViewModel());
        pages.Add(new SettingsPageViewModel());
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(History));
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        appearance.ToggleLanguage();
        if (ErrorMessage is not null) ErrorMessage = appearance.Text("StorageError");
        RefreshPages();
    }

    [RelayCommand]
    private void ToggleTheme() => appearance.ToggleTheme();
}
