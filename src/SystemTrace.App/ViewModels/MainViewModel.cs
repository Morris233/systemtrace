using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SystemTrace.App.Services;
using SystemTrace.Core;

namespace SystemTrace.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISystemScanSession session;
    private readonly IApplicationServices? application;
    private readonly IHistoryReader reader;
    private readonly IAppearanceService appearance;
    private readonly HistoryPresenter presenter;
    private readonly ILogger<MainViewModel> logger;
    private readonly CancellationTokenSource lifetime = new();
    private bool initialized;
    private string? errorKey;
    private void ShowError(string? key) { errorKey = key; ErrorMessage = key is null ? null : presenter.Text(key); }
    private bool relabeling;
    private DashboardSummary dashboard = DashboardSummary.Empty;
    private PageResult<ChangePreview> timelinePage = new([], 0, 0, 40);
    private PageResult<ChangePreview> troublePage = new([], 0, 0, 40);
    private PageResult<SnapshotHeader> snapshotPage = new([], 0, 0, 40);
    private PageResult<Change> comparisonPage = new([], 0, 0, 40);
    private ChangeQuery timelineFilter = new();
    private ChangeQuery troubleFilter = new();
    private readonly Dictionary<string, Change> comparedEvidence = [];

    public IReadOnlyList<object> Pages { get; }
    public DashboardSummary Dashboard => dashboard;
    public object CurrentPage => Pages.ElementAtOrDefault(SelectedPageIndex) ?? Pages[0];
    public XmlLanguage DisplayLanguage => XmlLanguage.GetLanguage(appearance.Language);
    public ObservableCollection<SnapshotOption> SnapshotChoices { get; } = [];
    public string LastAttemptText => presenter.Time(dashboard.LastAttemptUtc);
    public string LastCompleteText => presenter.Time(dashboard.LastCompleteUtc);
    public string TimelinePageText => presenter.Page(timelinePage.Page, timelinePage.PageCount);
    public string TimelineCountText => presenter.Count(timelinePage.Total);
    public string TroublePageText => presenter.Page(troublePage.Page, troublePage.PageCount);
    public string TroubleCountText => presenter.Count(troublePage.Total);
    public string SnapshotPageText => presenter.Page(snapshotPage.Page, snapshotPage.PageCount);
    public string ComparisonPageText => presenter.Page(comparisonPage.Page, comparisonPage.PageCount);
    public string ComparisonCountText => presenter.Count(comparisonPage.Total);
    public string ComparisonStateText => presenter.Text(HasCompared ? "NoChanges" : "ChooseSnapshots");

    [ObservableProperty][NotifyPropertyChangedFor(nameof(CurrentPage))] private int selectedPageIndex;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private IReadOnlyList<CategoryRow> categoryRows = [];
    [ObservableProperty] private IReadOnlyList<ChangeRow> recentRows = [];
    [ObservableProperty] private IReadOnlyList<ChangeRow> timelineRows = [];
    [ObservableProperty] private IReadOnlyList<ChangeRow> troubleRows = [];
    [ObservableProperty] private IReadOnlyList<ChangeRow> comparisonRows = [];
    [ObservableProperty] private IReadOnlyList<CategoryOption> filterOptions = [];
    [ObservableProperty] private IReadOnlyList<CategoryOption> snapshotCategories = [];
    [ObservableProperty] private IReadOnlyList<WindowOption> windowOptions = [];
    [ObservableProperty] private CategoryOption? timelineCategory;
    [ObservableProperty] private CategoryOption? troubleCategory;
    [ObservableProperty] private CategoryOption? snapshotCategory;
    [ObservableProperty] private WindowOption? selectedWindow;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string troubleText = "";
    [ObservableProperty] private DateTime? incidentDate = DateTime.Today;
    [ObservableProperty] private string incidentTime = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    [ObservableProperty] private ChangeRow? selectedTimelineRow;
    [ObservableProperty] private ChangeRow? selectedTroubleRow;
    [ObservableProperty] private ChangeRow? selectedComparisonRow;
    [ObservableProperty] private ChangeDetail? timelineDetail;
    [ObservableProperty] private ChangeDetail? troubleDetail;
    [ObservableProperty] private ChangeDetail? comparisonDetail;
    [ObservableProperty] private SnapshotOption? beforeSnapshot;
    [ObservableProperty] private SnapshotOption? afterSnapshot;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(ComparisonStateText))] private bool hasCompared;

    public MainViewModel(ISystemScanSession session, IHistoryReader reader, IAppearanceService appearance, ILogger<MainViewModel> logger, IApplicationServices? application = null)
    {
        this.session = session;
        this.application = application;
        this.reader = reader;
        this.appearance = appearance;
        presenter = new HistoryPresenter(appearance);
        this.logger = logger;
        Pages = Array.AsReadOnly<object>([new OverviewPageViewModel(this), new TimelinePageViewModel(this),
            new SnapshotsPageViewModel(this), new TroubleshootPageViewModel(this), new SettingsPageViewModel(this)]);
        Relabel();
        LoadPreferences();
    }

    public Task InitializeAsync() => WorkAsync(async token =>
    {
        dashboard = await reader.GetDashboardAsync(token);
        await LoadTimelineAsync(token);
        await LoadSnapshotsAsync(0, token);
        RelabelDashboard();
        initialized = true;
    });

    public void CancelPending() { lifetime.Cancel(); ScanCommand.Cancel(); }
    private bool CanWork() => !IsBusy && !lifetime.IsCancellationRequested;
    partial void OnIsBusyChanged(bool value) => NotifyCommands();
    private void NotifyCommands()
    {
        foreach (var command in new IRelayCommand[] { ScanCommand, SearchCommand, PreviousTimelineCommand, NextTimelineCommand,
            SearchTroubleCommand, PreviousTroubleCommand, NextTroubleCommand, LoadSnapshotsCommand, PreviousSnapshotsCommand,
            NextSnapshotsCommand, CompareCommand, PreviousComparisonCommand, NextComparisonCommand, OpenTimelineCommand,
            OpenTroubleCommand, OpenRecentCommand, ApplyPreferencesCommand, PruneHistoryCommand, ClearHistoryCommand, ExportJsonCommand, ExportCsvCommand }) command.NotifyCanExecuteChanged();
    }

    private async Task WorkAsync(Func<CancellationToken, Task> action, CancellationToken token = default)
    {
        if (!CanWork()) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        IsBusy = true;
        ShowError(null);
        StatusMessage = null;
        try { await action(linked.Token); }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "History operation failed");
            ShowError("StorageError");
        }
        finally { IsBusy = false; NotifyCommands(); }
    }

    [RelayCommand(CanExecute = nameof(CanWork), IncludeCancelCommand = true)]
    private Task ScanAsync(CancellationToken token) => WorkAsync(async ct =>
    {
        IsScanning = true; CompletedCollectors = 0; ScanningCategory = InventoryCategory.Software;
        try
        {
            if (session is IProgressScanSession reporting)
                await reporting.RunAsync(new Progress<ScanProgress>(progress =>
                {
                    if (!IsScanning) return;
                    CompletedCollectors = progress.Completed; ScanningCategory = progress.Category;
                }), ct);
            else await session.RunAsync(ct);
            if (application is not null) await application.PruneAsync(ct);
        }
        finally { IsScanning = false; }
        dashboard = await reader.GetDashboardAsync(ct);
        RelabelDashboard();
        await LoadTimelineAsync(ct);
        await LoadSnapshotsAsync(snapshotPage.Page, ct);
        initialized = true;
    }, token);

    [RelayCommand(CanExecute = nameof(CanWork))]
    private Task SearchAsync() => WorkAsync(async token =>
    {
        timelineFilter = new ChangeQuery(SearchText.Trim(), TimelineCategory?.Value);
        await LoadTimelineAsync(token);
    });

    private async Task LoadTimelineAsync(CancellationToken token)
    {
        var result = await reader.QueryChangesAsync(timelineFilter, token);
        timelinePage = result;
        timelineFilter = timelineFilter with { Page = result.Page };
        SelectedTimelineRow = null;
        TimelineDetail = null;
        TimelineRows = result.Items.Select(presenter.Row).ToArray();
        NotifyPageLabels();
    }

    private bool CanPreviousTimeline() => CanWork() && timelinePage.Page > 0;
    private bool CanNextTimeline() => CanWork() && timelinePage.Page + 1 < timelinePage.PageCount;
    [RelayCommand(CanExecute = nameof(CanPreviousTimeline))]
    private Task PreviousTimelineAsync() => WorkAsync(async token => { timelineFilter = timelineFilter with { Page = timelinePage.Page - 1 }; await LoadTimelineAsync(token); });
    [RelayCommand(CanExecute = nameof(CanNextTimeline))]
    private Task NextTimelineAsync() => WorkAsync(async token => { timelineFilter = timelineFilter with { Page = timelinePage.Page + 1 }; await LoadTimelineAsync(token); });

    partial void OnSelectedTimelineRowChanged(ChangeRow? value)
    { if (!relabeling && value is not null && CanWork()) _ = OpenTimelineCommand.ExecuteAsync(value); }
    partial void OnSelectedTroubleRowChanged(ChangeRow? value)
    { if (!relabeling && value is not null && CanWork()) _ = OpenTroubleCommand.ExecuteAsync(value); }
    partial void OnSelectedComparisonRowChanged(ChangeRow? value)
    {
        if (relabeling) return;
        ComparisonDetail = value is not null && comparedEvidence.TryGetValue(value.Preview.Id, out var change) ? presenter.Detail(change) : null;
    }

    [RelayCommand(CanExecute = nameof(CanWork))]
    private Task OpenTimelineAsync(ChangeRow? row) => WorkAsync(async token =>
    { if (row is not null) TimelineDetail = await DetailAsync(row.Preview.Id, token); });
    [RelayCommand(CanExecute = nameof(CanWork))]
    private Task OpenTroubleAsync(ChangeRow? row) => WorkAsync(async token =>
    { if (row is not null) TroubleDetail = await DetailAsync(row.Preview.Id, token); });
    [RelayCommand(CanExecute = nameof(CanWork))]
    private Task OpenRecentAsync(ChangeRow? row) => WorkAsync(async token =>
    {
        if (row is null) return;
        SearchText = "";
        TimelineCategory = FilterOptions[0];
        timelineFilter = new();
        await LoadTimelineAsync(token);
        SelectedPageIndex = 1;
        SelectedTimelineRow = TimelineRows.FirstOrDefault(r => r.Preview.Id == row.Preview.Id);
        TimelineDetail = await DetailAsync(row.Preview.Id, token);
    });
    private async Task<ChangeDetail?> DetailAsync(string id, CancellationToken token)
    {
        var evidence = await reader.GetChangeAsync(id, token);
        if (evidence is null) { ShowError("RecordUnavailable"); return null; }
        return presenter.Detail(evidence);
    }

    [RelayCommand(CanExecute = nameof(CanWork))]
    private Task SearchTroubleAsync() => WorkAsync(async token =>
    {
        if (!TryIncident(out var incident)) { ShowError("InvalidIncidentTime"); return; }
        troubleFilter = new ChangeQuery(TroubleText.Trim(), TroubleCategory?.Value,
            incident.AddHours(-(SelectedWindow?.Hours ?? 24)), incident);
        await LoadTroubleAsync(token);
    });
    private bool TryIncident(out DateTimeOffset incident)
    {
        incident = default;
        if (IncidentDate is not { } date || !TimeSpan.TryParseExact(IncidentTime, [@"hh\:mm", @"hh\:mm\:ss"], CultureInfo.InvariantCulture, out var time)) return false;
        var local = DateTime.SpecifyKind(date.Date + time, DateTimeKind.Unspecified);
        if (TimeZoneInfo.Local.IsInvalidTime(local) || TimeZoneInfo.Local.IsAmbiguousTime(local)) return false;
        incident = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUniversalTime();
        return true;
    }
    private async Task LoadTroubleAsync(CancellationToken token)
    {
        troublePage = await reader.QueryChangesAsync(troubleFilter, token);
        troubleFilter = troubleFilter with { Page = troublePage.Page };
        SelectedTroubleRow = null;
        TroubleDetail = null;
        TroubleRows = troublePage.Items.Select(presenter.Row).ToArray();
        NotifyPageLabels();
    }
    private bool CanPreviousTrouble() => CanWork() && troublePage.Page > 0;
    private bool CanNextTrouble() => CanWork() && troublePage.Page + 1 < troublePage.PageCount;
    [RelayCommand(CanExecute = nameof(CanPreviousTrouble))]
    private Task PreviousTroubleAsync() => WorkAsync(async token => { troubleFilter = troubleFilter with { Page = troublePage.Page - 1 }; await LoadTroubleAsync(token); });
    [RelayCommand(CanExecute = nameof(CanNextTrouble))]
    private Task NextTroubleAsync() => WorkAsync(async token => { troubleFilter = troubleFilter with { Page = troublePage.Page + 1 }; await LoadTroubleAsync(token); });

    partial void OnSnapshotCategoryChanged(CategoryOption? oldValue, CategoryOption? newValue)
    {
        if (relabeling || newValue is null || oldValue?.Value == newValue.Value) return;
        BeforeSnapshot = null;
        AfterSnapshot = null;
        SnapshotChoices.Clear();
        ResetComparison();
        if (initialized && CanWork()) _ = LoadSnapshotsCommand.ExecuteAsync(null);
    }
    partial void OnBeforeSnapshotChanged(SnapshotOption? value) { ResetComparison(); NotifyCommands(); }
    partial void OnAfterSnapshotChanged(SnapshotOption? value) { ResetComparison(); NotifyCommands(); }
    private void ResetComparison()
    {
        HasCompared = false;
        ComparisonRows = [];
        SelectedComparisonRow = null;
        ComparisonDetail = null;
        comparedEvidence.Clear();
        comparisonPage = new([], 0, 0, 40);
        NotifyPageLabels();
    }

    [RelayCommand(CanExecute = nameof(CanWork))]
    private Task LoadSnapshotsAsync() => WorkAsync(token => LoadSnapshotsAsync(0, token));
    private async Task LoadSnapshotsAsync(int page, CancellationToken token)
    {
        snapshotPage = await reader.QuerySnapshotsAsync(SnapshotCategory?.Value ?? InventoryCategory.Software, page, cancellationToken: token);
        var desired = snapshotPage.Items.Select(header => new SnapshotOption(header))
            .Concat(new[] { BeforeSnapshot, AfterSnapshot }.OfType<SnapshotOption>())
            .DistinctBy(option => option.Header.Id).OrderByDescending(option => option.Header.ObservedAtUtc).ThenByDescending(option => option.Header.Id).ToArray();
        foreach (var option in desired)
            if (!SnapshotChoices.Any(old => old.Header.Id == option.Header.Id)) SnapshotChoices.Add(option);
        foreach (var obsolete in SnapshotChoices.Where(old => !desired.Any(option => option.Header.Id == old.Header.Id)).ToArray()) SnapshotChoices.Remove(obsolete);
        for (var index = 0; index < desired.Length; index++)
        {
            var current = SnapshotChoices.First(option => option.Header.Id == desired[index].Header.Id);
            var position = SnapshotChoices.IndexOf(current);
            if (position != index) SnapshotChoices.Move(position, index);
        }
        NotifyPageLabels();
    }
    private bool CanPreviousSnapshots() => CanWork() && snapshotPage.Page > 0;
    private bool CanNextSnapshots() => CanWork() && snapshotPage.Page + 1 < snapshotPage.PageCount;
    [RelayCommand(CanExecute = nameof(CanPreviousSnapshots))]
    private Task PreviousSnapshotsAsync() => WorkAsync(token => LoadSnapshotsAsync(snapshotPage.Page - 1, token));
    [RelayCommand(CanExecute = nameof(CanNextSnapshots))]
    private Task NextSnapshotsAsync() => WorkAsync(token => LoadSnapshotsAsync(snapshotPage.Page + 1, token));
    private bool CanCompare() => CanWork() && BeforeSnapshot is { } before && AfterSnapshot is { } after
        && before.Header.Id != after.Header.Id && before.Header.Category == after.Header.Category && before.Header.ObservedAtUtc <= after.Header.ObservedAtUtc;
    [RelayCommand(CanExecute = nameof(CanCompare))]
    private Task CompareAsync() => WorkAsync(async token => { if (ValidPair()) await LoadComparisonAsync(0, token); });
    private bool ValidPair() => BeforeSnapshot is { } before && AfterSnapshot is { } after && before.Header.Id != after.Header.Id
        && before.Header.Category == after.Header.Category && before.Header.ObservedAtUtc <= after.Header.ObservedAtUtc;
    private async Task LoadComparisonAsync(int page, CancellationToken token)
    {
        if (!ValidPair()) return;
        comparisonPage = await reader.CompareSnapshotsAsync(BeforeSnapshot!.Header.Id, AfterSnapshot!.Header.Id, page, cancellationToken: token);
        comparedEvidence.Clear();
        foreach (var change in comparisonPage.Items) comparedEvidence[change.Id] = change;
        SelectedComparisonRow = null;
        ComparisonDetail = null;
        ComparisonRows = comparisonPage.Items.Select(change => presenter.Row(ChangePreviews.Create(change))).ToArray();
        HasCompared = true;
        NotifyPageLabels();
    }
    private bool CanPreviousComparison() => CanWork() && HasCompared && comparisonPage.Page > 0;
    private bool CanNextComparison() => CanWork() && HasCompared && comparisonPage.Page + 1 < comparisonPage.PageCount;
    [RelayCommand(CanExecute = nameof(CanPreviousComparison))]
    private Task PreviousComparisonAsync() => WorkAsync(token => LoadComparisonAsync(comparisonPage.Page - 1, token));
    [RelayCommand(CanExecute = nameof(CanNextComparison))]
    private Task NextComparisonAsync() => WorkAsync(token => LoadComparisonAsync(comparisonPage.Page + 1, token));

    private void RelabelDashboard()
    {
        CategoryRows = dashboard.Categories.Select(presenter.Category).ToArray();
        RecentRows = dashboard.RecentChanges.Select(presenter.Row).ToArray();
        OnPropertyChanged(nameof(Dashboard));
        OnPropertyChanged(nameof(LastAttemptText));
        OnPropertyChanged(nameof(LastCompleteText));
    }
    private void Relabel()
    {
        relabeling = true;
        var timelineEvidence = TimelineDetail?.Evidence;
        var troubleEvidence = TroubleDetail?.Evidence;
        var comparisonEvidence = ComparisonDetail?.Evidence;
        var timelineSelected = SelectedTimelineRow?.Preview.Id;
        var troubleSelected = SelectedTroubleRow?.Preview.Id;
        var comparisonSelected = SelectedComparisonRow?.Preview.Id;
        try
        {
            var timelineCategoryValue = TimelineCategory?.Value;
            var troubleCategoryValue = TroubleCategory?.Value;
            var snapshotCategoryValue = SnapshotCategory?.Value ?? InventoryCategory.Software;
            var hours = SelectedWindow?.Hours ?? 24;
            FilterOptions = presenter.Categories(true);
            SnapshotCategories = presenter.Categories(false);
            WindowOptions = presenter.Windows();
            TimelineCategory = FilterOptions.First(o => o.Value == timelineCategoryValue);
            TroubleCategory = FilterOptions.First(o => o.Value == troubleCategoryValue);
            SnapshotCategory = SnapshotCategories.First(o => o.Value == snapshotCategoryValue);
            SelectedWindow = WindowOptions.First(o => o.Hours == hours);
            RelabelDashboard();
            TimelineRows = timelinePage.Items.Select(presenter.Row).ToArray();
            TroubleRows = troublePage.Items.Select(presenter.Row).ToArray();
            ComparisonRows = comparisonPage.Items.Select(change => presenter.Row(ChangePreviews.Create(change))).ToArray();
            SelectedTimelineRow = TimelineRows.FirstOrDefault(row => row.Preview.Id == timelineSelected);
            SelectedTroubleRow = TroubleRows.FirstOrDefault(row => row.Preview.Id == troubleSelected);
            SelectedComparisonRow = ComparisonRows.FirstOrDefault(row => row.Preview.Id == comparisonSelected);
            TimelineDetail = timelineEvidence is null ? null : presenter.Detail(timelineEvidence);
            TroubleDetail = troubleEvidence is null ? null : presenter.Detail(troubleEvidence);
            ComparisonDetail = comparisonEvidence is null ? null : presenter.Detail(comparisonEvidence);
            if (errorKey is not null) ErrorMessage = presenter.Text(errorKey);
            StatusMessage = null;
            OnPropertyChanged(nameof(ScanProgressText));
            OnPropertyChanged(nameof(DisplayLanguage));
            NotifyPageLabels();
        }
        finally { relabeling = false; }
    }
    private void NotifyPageLabels()
    {
        foreach (var name in new[] { nameof(TimelinePageText), nameof(TimelineCountText), nameof(TroublePageText), nameof(TroubleCountText),
            nameof(SnapshotPageText), nameof(ComparisonPageText), nameof(ComparisonCountText), nameof(ComparisonStateText) }) OnPropertyChanged(name);
    }
    [RelayCommand(CanExecute = nameof(CanWork))] private async Task ToggleLanguageAsync() { appearance.ToggleLanguage(); Relabel(); await SaveAppearanceAsync(); }
    [RelayCommand(CanExecute = nameof(CanWork))] private async Task ToggleThemeAsync() { appearance.ToggleTheme(); await SaveAppearanceAsync(); }
}
