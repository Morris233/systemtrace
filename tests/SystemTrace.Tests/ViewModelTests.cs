using Microsoft.Extensions.Logging.Abstractions;
using SystemTrace.App.Services;
using SystemTrace.App.ViewModels;
using SystemTrace.Core;
using Xunit;

namespace SystemTrace.Tests;

public sealed class ViewModelTests
{
    [Fact]
    public async Task IoFailureIsRecoverableAndDoesNotReplaceHistory()
    {
        var vm = Create(new FailingSession());
        await vm.ScanCommand.ExecuteAsync(null);
        Assert.Equal("StorageError", vm.ErrorMessage);
        Assert.Equal(0, vm.Dashboard.TotalRuns);
        Assert.False(vm.IsBusy);
        Assert.True(vm.ScanCommand.CanExecute(null));
    }

    [Fact]
    public async Task PendingCommandDisablesReentryAndCancellationRestoresIdle()
    {
        var session = new WaitingSession();
        var vm = Create(session);
        var task = vm.ScanCommand.ExecuteAsync(null);
        await session.Started.Task;
        Assert.False(vm.ScanCommand.CanExecute(null));
        vm.ScanCommand.Cancel();
        await task;
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsBusy);
        Assert.True(vm.ScanCommand.CanExecute(null));
        vm.CancelPending();
        Assert.False(vm.ScanCommand.CanExecute(null));
    }

    [Fact]
    public async Task SnapshotSelectionsSurviveCataloguePagingAndComparisonDoesNotPersistEvents()
    {
        using var store = new TestStore(false);
        for (var step = 0; step < 43; step++) await store.Repository.CommitAsync(HistoryReaderTests.Run(step, 1));
        var vm = new MainViewModel(new ReadSession(store.Repository), store.Repository, new Appearance(), NullLogger<MainViewModel>.Instance);
        await vm.InitializeAsync();
        vm.BeforeSnapshot = vm.SnapshotChoices[1];
        vm.AfterSnapshot = vm.SnapshotChoices[0];
        var selected = (vm.BeforeSnapshot, vm.AfterSnapshot);
        await vm.NextSnapshotsCommand.ExecuteAsync(null);
        Assert.Equal(selected, (vm.BeforeSnapshot, vm.AfterSnapshot));
        Assert.Equal(5, vm.SnapshotChoices.Count); // three older headers plus the selected pair
        Assert.True(vm.CompareCommand.CanExecute(null));
        await vm.CompareCommand.ExecuteAsync(null);
        Assert.Single(vm.ComparisonRows);
        vm.SelectedComparisonRow = vm.ComparisonRows[0];
        Assert.NotNull(vm.ComparisonDetail);
        var count = store.Scalar("SELECT COUNT(*) FROM changes");
        await vm.PreviousSnapshotsCommand.ExecuteAsync(null);
        Assert.Equal(40, vm.SnapshotChoices.Count);
        Assert.Equal(selected, (vm.BeforeSnapshot, vm.AfterSnapshot));
        Assert.True(vm.HasCompared);
        Assert.Equal(count, store.Scalar("SELECT COUNT(*) FROM changes"));
        vm.AfterSnapshot = vm.BeforeSnapshot;
        Assert.False(vm.CompareCommand.CanExecute(null));
        Assert.False(vm.HasCompared);
        Assert.Null(vm.ComparisonDetail);
    }

    [Fact]
    public async Task SearchingAnotherPageClearsOldEvidenceAndRecentOpensItsPersistedRecord()
    {
        using var store = new TestStore(false);
        await store.Repository.CommitAsync(HistoryReaderTests.Run(0, 60));
        await store.Repository.CommitAsync(HistoryReaderTests.Run(1, 60));
        var vm = new MainViewModel(new ReadSession(store.Repository), store.Repository, new Appearance(), NullLogger<MainViewModel>.Instance);
        await vm.InitializeAsync();
        await vm.OpenTimelineCommand.ExecuteAsync(vm.TimelineRows[0]);
        Assert.NotNull(vm.TimelineDetail);
        await vm.NextTimelineCommand.ExecuteAsync(null);
        Assert.Null(vm.TimelineDetail);
        vm.SearchText = "nonexistent";
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.Empty(vm.TimelineRows);
        Assert.Null(vm.SelectedTimelineRow);
        Assert.False(vm.NextTimelineCommand.CanExecute(null));
        var recent = vm.RecentRows[0];
        await vm.OpenRecentCommand.ExecuteAsync(recent);
        Assert.Equal(1, vm.SelectedPageIndex);
        Assert.Equal("", vm.SearchText);
        Assert.Equal(recent.Preview.Id, vm.TimelineDetail!.Evidence.Id);
        Assert.Equal(40, vm.TimelineRows.Count);
    }

    [Fact]
    public void PresenterDistinguishesMissingNullEmptyUnreadableAndDeviceAbsence()
    {
        var presenter = new HistoryPresenter(new Appearance());
        var before = new SnapshotItem("device", new Dictionary<string, FieldValue>
        {
            ["null"] = new(null),
            ["empty"] = new(""),
            ["error"] = new(null, "AccessDenied"),
            ["removed"] = new("old")
        });
        var after = new SnapshotItem("device", new Dictionary<string, FieldValue> { ["new"] = new("new") });
        var change = new Change("id", InventoryCategory.Drivers, "device", ChangeKind.Removed,
            HistoryReaderTests.Time, HistoryReaderTests.Time.AddHours(1), before, after,
            [new FieldDelta("null", new(null), null)]);
        var detail = presenter.Detail(change);
        Assert.Equal("NotDetected", detail.Kind);
        Assert.Equal("ValueNull", detail.Fields.Single(f => f.Name == "null").Before);
        Assert.Equal("ValueEmpty", detail.Fields.Single(f => f.Name == "empty").Before);
        var unreadable = detail.Fields.Single(f => f.Name == "error");
        Assert.Equal("ValueUnreadable", unreadable.Before);
        Assert.Equal("AccessDenied", unreadable.BeforeError);
        Assert.Equal("ValueMissing", detail.Fields.Single(f => f.Name == "removed").After);
        Assert.Equal("null", detail.Fields[0].Name);
    }

    private sealed class ReadSession(IScanRepository repository) : ISystemScanSession
    {
        public Task<InventoryHistory> LoadAsync(CancellationToken cancellationToken = default) => repository.LoadAsync(cancellationToken);
        public Task<InventoryHistory> RunAsync(CancellationToken cancellationToken = default) => repository.LoadAsync(cancellationToken);
    }

    private static MainViewModel Create(ISystemScanSession session) => new(session, new EmptyReader(), new Appearance(), NullLogger<MainViewModel>.Instance);
    internal sealed class EmptyReader : IHistoryReader
    {
        public Task<DashboardSummary> GetDashboardAsync(CancellationToken cancellationToken = default) => Task.FromResult(DashboardSummary.Empty);
        public Task<PageResult<ChangePreview>> QueryChangesAsync(ChangeQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new PageResult<ChangePreview>([], 0, 0, query.PageSize));
        public Task<Change?> GetChangeAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<Change?>(null);
        public Task<PageResult<SnapshotHeader>> QuerySnapshotsAsync(InventoryCategory category, int page = 0, int pageSize = 40, CancellationToken cancellationToken = default) => Task.FromResult(new PageResult<SnapshotHeader>([], 0, 0, pageSize));
        public Task<PageResult<Change>> CompareSnapshotsAsync(Guid beforeId, Guid afterId, int page = 0, int pageSize = 40, CancellationToken cancellationToken = default) => Task.FromResult(new PageResult<Change>([], 0, 0, pageSize));
    }
    private sealed class Appearance : IAppearanceService
    {
        public string Language => "en-US";
        public bool IsDark => true;
        public string Text(string key) => key;
        public void ToggleLanguage() { }
        public void ToggleTheme() { }
    }
    private sealed class FailingSession : ISystemScanSession
    {
        public Task<InventoryHistory> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(InventoryHistory.Empty);
        public Task<InventoryHistory> RunAsync(CancellationToken cancellationToken = default) => throw new System.IO.IOException("Simulated I/O failure");
    }
    private sealed class WaitingSession : ISystemScanSession
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<InventoryHistory> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(InventoryHistory.Empty);
        public async Task<InventoryHistory> RunAsync(CancellationToken cancellationToken = default)
        {
            Started.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return InventoryHistory.Empty;
        }
    }
}
