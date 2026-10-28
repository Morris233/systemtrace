using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SystemTrace.App.Services;
using SystemTrace.App.ViewModels;
using SystemTrace.Core;
using Xunit;

namespace SystemTrace.Tests;

public sealed class TimelineFilterTests
{
    [Theory]
    [InlineData(ChangeKind.Added, 1)]
    [InlineData(ChangeKind.Removed, 2)]
    [InlineData(ChangeKind.Modified, 89)]
    public async Task KindFilterIntersectsTextCategoryAndWindowAndExportsEveryPage(ChangeKind kind, int total)
    {
        using var store = new TestStore(false);
        await store.Repository.CommitAsync(HistoryReaderTests.Run(0, 45));
        await store.Repository.CommitAsync(HistoryReaderTests.Run(1, 46));
        await store.Repository.CommitAsync(HistoryReaderTests.Run(2, 44));
        var query = new ChangeQuery(Category: InventoryCategory.Software, PageSize: 1, Page: 1, Kind: kind);
        var page = await store.Repository.QueryChangesAsync(query);
        Assert.Equal(total, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(kind, page.Items[0].Kind);
        Assert.Equal(Math.Min(1, total - 1), page.Page);
        Assert.Equal(4 * total, (await store.Repository.QueryChangesAsync(query with { Category = null })).Total);
        var all = await store.Repository.QueryChangesAsync(new(Category: InventoryCategory.Software, PageSize: 100));
        var intersected = await store.Repository.QueryChangesAsync(query with
        {
            Text = "Application",
            FromUtc = HistoryReaderTests.Time.AddMinutes(2),
            PageSize = 100,
            Page = 0
        });
        var expected = all.Items.Where(item => item.Kind == kind && item.Label.StartsWith("Application", StringComparison.Ordinal)
            && item.ObservedToUtc >= HistoryReaderTests.Time.AddMinutes(2)).Select(item => item.Id);
        Assert.Equal(expected, intersected.Items.Select(item => item.Id));

        using var json = new MemoryStream();
        await store.Repository.ExportAsync(json, ReportFormat.Json, query);
        using var document = JsonDocument.Parse(json.ToArray());
        var changes = document.RootElement.GetProperty("changes").EnumerateArray().ToArray();
        Assert.Equal(total, changes.Length);
        Assert.All(changes, item =>
        {
            Assert.Equal(kind.ToString(), item.GetProperty("Kind").GetString());
            Assert.Equal("Software", item.GetProperty("Category").GetString());
        });
        using var csv = new MemoryStream();
        await store.Repository.ExportAsync(csv, ReportFormat.Csv, query);
        var lines = Encoding.UTF8.GetString(csv.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
        Assert.Equal(total * 4, lines.Length); // Four evidence fields per fixture record.
        Assert.All(lines, line => Assert.StartsWith($"\"Software\",\"{kind}\",", line));
    }

    [Fact]
    public async Task InvalidKindIsRejectedBeforeReadingOrExporting()
    {
        using var store = new TestStore(false);
        var query = new ChangeQuery(Kind: (ChangeKind)999);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.Repository.QueryChangesAsync(query));
        using var output = new MemoryStream();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.Repository.ExportAsync(output, ReportFormat.Json, query));
        Assert.Equal(0, output.Length);
        Assert.False(File.Exists(store.DatabasePath));
    }

    [Fact]
    public async Task ResetRestoresFirstPageClearsDetailAndAppliedExportFilter()
    {
        using var store = new TestStore(false);
        await store.Repository.CommitAsync(HistoryReaderTests.Run(0, 45));
        await store.Repository.CommitAsync(HistoryReaderTests.Run(1, 45));
        var application = new TestApplicationServices();
        var vm = Create(store, application);
        await vm.InitializeAsync();
        vm.TimelineKind = vm.KindOptions.Single(option => option.Value == ChangeKind.Modified);
        vm.TimelineCategory = vm.FilterOptions.Single(option => option.Value == InventoryCategory.Software);
        await vm.SearchCommand.ExecuteAsync(null);
        await vm.NextTimelineCommand.ExecuteAsync(null);
        await vm.OpenTimelineCommand.ExecuteAsync(vm.TimelineRows[0]);
        Assert.NotNull(vm.TimelineDetail);
        await vm.ExportCsvCommand.ExecuteAsync(null);
        Assert.Equal(ChangeKind.Modified, application.ExportedQuery!.Kind);
        await vm.ResetTimelineCommand.ExecuteAsync(null);
        Assert.Null(vm.TimelineKind!.Value);
        Assert.Null(vm.TimelineCategory!.Value);
        Assert.Null(vm.TimelineDetail);
        Assert.Null(vm.SelectedTimelineRow);
        Assert.Equal("", vm.SearchText);
        Assert.Equal(40, vm.TimelineRows.Count);
        Assert.False(vm.PreviousTimelineCommand.CanExecute(null));
        await vm.ExportJsonCommand.ExecuteAsync(null);
        Assert.Equal(new ChangeQuery(), application.ExportedQuery);

        vm.TimelineKind = vm.KindOptions.Single(option => option.Value == ChangeKind.Added);
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.Empty(vm.TimelineRows);
        Assert.Equal("NoMatches", vm.TimelineStateText);
        await vm.OpenRecentCommand.ExecuteAsync(vm.RecentRows[0]);
        Assert.Null(vm.TimelineKind.Value);
        Assert.NotNull(vm.TimelineDetail);
        Assert.Equal("NoChanges", vm.TimelineStateText);
    }

    [Fact]
    public async Task FailedSearchPreservesVisibleRowsAndLastAppliedExportQuery()
    {
        using var store = new TestStore(false);
        await store.Repository.CommitAsync(HistoryReaderTests.Run(0));
        await store.Repository.CommitAsync(HistoryReaderTests.Run(1));
        var application = new TestApplicationServices();
        var vm = Create(store, application);
        await vm.InitializeAsync();
        vm.SearchText = "Application";
        vm.TimelineKind = vm.KindOptions.Single(option => option.Value == ChangeKind.Modified);
        await vm.SearchCommand.ExecuteAsync(null);
        var visible = vm.TimelineRows;
        await vm.ExportJsonCommand.ExecuteAsync(null);
        var applied = application.ExportedQuery;
        vm.SearchText = new string('x', 513);
        vm.TimelineKind = vm.KindOptions.Single(option => option.Value == ChangeKind.Added);
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.Equal("StorageError", vm.ErrorMessage);
        Assert.Same(visible, vm.TimelineRows);
        await vm.ExportJsonCommand.ExecuteAsync(null);
        Assert.Equal(applied, application.ExportedQuery);
    }

    private static MainViewModel Create(TestStore store, TestApplicationServices application) =>
        new(new Session(store.Repository), store.Repository, new Appearance(), NullLogger<MainViewModel>.Instance, application);

    private sealed class Session(IScanRepository repository) : ISystemScanSession
    {
        public Task<InventoryHistory> LoadAsync(CancellationToken cancellationToken = default) => repository.LoadAsync(cancellationToken);
        public Task<InventoryHistory> RunAsync(CancellationToken cancellationToken = default) => repository.LoadAsync(cancellationToken);
    }

    private sealed class Appearance : IAppearanceService
    {
        public string Language => "en-US";
        public bool IsDark => true;
        public string Text(string key) => key;
        public void ToggleLanguage() { }
        public void ToggleTheme() { }
    }
}
