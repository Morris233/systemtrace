using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SystemTrace.App.Services;
using SystemTrace.Core;

namespace SystemTrace.App.ViewModels;

public sealed record CollectorRow(string StatusText, string WarningText);
public sealed record OverviewPageViewModel(DemoOverview Overview, string StatusText, string DetailText, IReadOnlyList<CollectorRow> Results, string CountCaption);
public sealed record ChangeRow(string Label, string KindText, string ValueText, string ObservationWindow);
public sealed record TimelinePageViewModel(IReadOnlyList<ChangeRow> Rows);
public sealed record SnapshotRow(string CategoryText, string ItemCount, string ObservedAt);
public sealed record SnapshotsPageViewModel(IReadOnlyList<SnapshotRow> Rows);
public sealed record TroubleshootPageViewModel;
public sealed record SettingsPageViewModel;

public partial class MainViewModel : ObservableObject
{
    private readonly IDemoSession session;
    private readonly ISystemScanSession? systemSession;
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
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleModeCommand))]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    public bool IsDemo { get; private set; }
    public string NoticeText => appearance.Text(IsDemo ? "DemoNotice" : "LocalNotice");
    public string ScanButtonText => appearance.Text(IsDemo ? "RunDemo" : "ScanNow");
    public string ModeToggleText => appearance.Text(IsDemo ? "SystemMode" : "DemoMode");

    public object CurrentPage => pages.ElementAtOrDefault(SelectedPageIndex) ?? pages[0];

    public MainViewModel(IDemoSession session, IAppearanceService appearance, ILogger<MainViewModel> logger, ISystemScanSession? systemSession = null)
    {
        this.session = session;
        this.systemSession = systemSession;
        IsDemo = systemSession is null;
        this.appearance = appearance;
        this.logger = logger;
        RefreshPages();
    }

    public Task InitializeAsync() => ExecuteAsync(IsDemo ? session.LoadAsync : systemSession!.LoadAsync, lifetime.Token);
    public void CancelPending()
    {
        lifetime.Cancel();
        ScanCommand.Cancel();
    }

    private bool CanScan() => !IsLoading && !lifetime.IsCancellationRequested;

    [RelayCommand(CanExecute = nameof(CanScan), IncludeCancelCommand = true)]
    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        await ExecuteAsync(IsDemo ? session.RunAsync : systemSession!.RunAsync, linked.Token);
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
            logger.LogError(exception, "Inventory history operation failed");
            ErrorMessage = appearance.Text("StorageError");
        }
        finally { IsLoading = false; }
    }

    private void RefreshPages()
    {
        pages.Clear();
        var overview = new DemoOverview(IsDemo, Array.AsReadOnly(Enum.GetValues<InventoryCategory>()
            .Select(category => new DemoCategory(category.ToString(), History.ChangeCounts.GetValueOrDefault(category))).ToArray()));
        var hasBaseline = History.Baselines.Count > 0;
        var detail = History.LastAttempt is { } last
            ? appearance.Text(IsDemo ? "LastDemo" : "LastAttempt") + " " + last.CompletedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)
            : appearance.Text(IsDemo ? "RunDemoHint" : "ScanHint");
        var states = Enum.GetValues<InventoryCategory>().Select(category =>
        {
            var result = History.LastAttempt?.Results.Single(r => r.Category == category);
            var warnings = result?.Warnings.Select(code => appearance.Text(IsKnownWarning(code) ? code : "ReadWarning"));
            var warning = string.Join("\n", warnings ?? []);
            if (result?.ErrorCode is not null && warning.Length == 0) warning = appearance.Text("ReadWarning");
            if (result?.SkippedItemCount > 0) warning += "\n" + string.Format(CultureInfo.CurrentCulture, appearance.Text("SkippedItems"), result.SkippedItemCount);
            return new CollectorRow(appearance.Text(result?.Status.ToString() ?? "NotScanned"), warning.Trim());
        }).ToArray();
        pages.Add(new OverviewPageViewModel(overview, appearance.Text(hasBaseline ? "BaselineReady" : "BaselineTitle"), detail, states,
            appearance.Text(IsDemo ? "ExampleChanges" : "ChangeCount")));
        pages.Add(new TimelinePageViewModel(History.Changes.Select(change =>
        {
            var item = change.After ?? change.Before!;
            var label = item.Fields.GetValueOrDefault("label_key")?.Value is { } labelKey ? appearance.Text(labelKey)
                : item.Fields.GetValueOrDefault("display_name")?.Value ?? appearance.Text(change.Category.ToString());
            var priority = new[] { "value", "version", "driver_version", "operational_status", "command", "display_name" };
            var fieldName = priority.FirstOrDefault(name => change.Fields.Any(field => field.Name == name)) ?? change.Fields.FirstOrDefault()?.Name;
            var oldValue = fieldName is null ? null : change.Before?.Fields.GetValueOrDefault(fieldName);
            var newValue = fieldName is null ? null : change.After?.Fields.GetValueOrDefault(fieldName);
            string Value(FieldValue? field) => field is null ? "—" : field.Value is null ? "∅"
                : fieldName is "value" or "operational_status" ? appearance.Text(field.Value) : field.Value;
            var kindKey = change.Category == InventoryCategory.Drivers && change.Kind == ChangeKind.Removed ? "NotDetected" : change.Kind.ToString();
            return new ChangeRow(label, appearance.Text(kindKey), $"{Value(oldValue)} → {Value(newValue)}",
                $"{change.ObservedFromUtc.ToLocalTime():MM-dd HH:mm:ss} → {change.ObservedToUtc.ToLocalTime():MM-dd HH:mm:ss}");
        }).ToArray()));
        pages.Add(new SnapshotsPageViewModel(History.Baselines.Select(snapshot => new SnapshotRow(
            appearance.Text(snapshot.Category.ToString()), string.Format(CultureInfo.CurrentCulture, appearance.Text("ItemCountFormat"), snapshot.Items.Count),
            snapshot.ObservedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture))).ToArray()));
        pages.Add(new TroubleshootPageViewModel());
        pages.Add(new SettingsPageViewModel());
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(History));
        OnPropertyChanged(nameof(IsDemo));
        OnPropertyChanged(nameof(NoticeText));
        OnPropertyChanged(nameof(ScanButtonText));
        OnPropertyChanged(nameof(ModeToggleText));
    }

    private static bool IsKnownWarning(string code) => code is "SoftwareCoverageLimited" or "StartupCoverageLimited"
        or "RunOnceTransient" or "PresentDevicesOnly" or "NetworkOperationalStatusOnly" or "AccessDenied";

    private bool CanToggleMode() => !IsLoading && systemSession is not null && !lifetime.IsCancellationRequested;

    [RelayCommand(CanExecute = nameof(CanToggleMode))]
    private async Task ToggleModeAsync()
    {
        IsDemo = !IsDemo;
        History = InventoryHistory.Empty;
        ErrorMessage = null;
        RefreshPages();
        await InitializeAsync();
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
