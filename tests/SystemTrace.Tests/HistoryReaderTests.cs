using Microsoft.Data.Sqlite;
using SystemTrace.Core;
using Xunit;

namespace SystemTrace.Tests;

public sealed class HistoryReaderTests
{
    internal static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-10-26T00:00:00Z");
    internal static ScanRun Run(int step, int items = 3, CollectionStatus software = CollectionStatus.Succeeded)
    {
        var time = Time.AddMinutes(step);
        return new ScanRun(Guid.NewGuid(), time, time, false,
            Enum.GetValues<InventoryCategory>().Select(c =>
            {
                var status = c == InventoryCategory.Software ? software : CollectionStatus.Succeeded;
                return new CollectionResult(c, status, time, time,
                    status is CollectionStatus.Failed or CollectionStatus.Skipped ? [] :
                    Enumerable.Range(0, items).Select(i => new SnapshotItem(c + "/" + i, new Dictionary<string, FieldValue>
                    {
                        ["display_name"] = new(i == 0 ? "Café %_';--" : "Application " + i),
                        ["version"] = new(step.ToString()),
                        ["nullable"] = new(step == 0 ? null : ""),
                        ["description"] = new(new string('x', 400))
                    })).ToArray(), status == CollectionStatus.Succeeded ? null : "AccessDenied");
            }).ToArray());
    }

    [Fact]
    public async Task PagedHistoryIsCompleteStableAndUsesLiteralNormalizedSearch()
    {
        using var store = new TestStore(false);
        await store.Repository.CommitAsync(Run(0, 300));
        await store.Repository.CommitAsync(Run(1, 300));
        await store.Repository.CommitAsync(Run(2, 300));
        var first = await store.Repository.QueryChangesAsync(new(Category: InventoryCategory.Software));
        var second = await store.Repository.QueryChangesAsync(new(Category: InventoryCategory.Software, Page: 1));
        Assert.Equal(600, first.Total);
        Assert.Equal(40, first.Items.Count);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)));
        Assert.Equal(first.Items, (await store.Repository.QueryChangesAsync(new(Category: InventoryCategory.Software))).Items);
        var last = await store.Repository.QueryChangesAsync(new(Category: InventoryCategory.Software, Page: int.MaxValue));
        Assert.Equal(14, last.Page);
        Assert.Equal(40, last.Items.Count);
        var literal = await store.Repository.QueryChangesAsync(new("cafe\u0301 %_';--", InventoryCategory.Software));
        Assert.Equal(2, literal.Total);
        Assert.Equal(2, (await store.Repository.QueryChangesAsync(new("%_';--", InventoryCategory.Software))).Total);
        Assert.Equal(2400, store.Scalar("SELECT COUNT(*) FROM changes"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.Repository.QueryChangesAsync(new(PageSize: 101)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.Repository.QueryChangesAsync(new(Page: -1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.Repository.QueryChangesAsync(new(new string('x', 513))));
        await Assert.ThrowsAsync<ArgumentException>(() => store.Repository.QueryChangesAsync(new(FromUtc: Time.AddDays(1), ToUtc: Time)));
    }

    [Fact]
    public async Task ObservationSearchIncludesOverlappingAndBoundaryWindows()
    {
        using var store = new TestStore(false);
        await store.Repository.CommitAsync(Run(0));
        await store.Repository.CommitAsync(Run(60));
        Assert.Equal(3, (await store.Repository.QueryChangesAsync(new(Category: InventoryCategory.Software,
            FromUtc: Time.AddMinutes(30), ToUtc: Time.AddMinutes(31)))).Total);
        Assert.Equal(12, (await store.Repository.QueryChangesAsync(new(FromUtc: Time.AddHours(1), ToUtc: Time.AddHours(1)))).Total);
        Assert.Equal(12, (await store.Repository.QueryChangesAsync(new(FromUtc: Time.ToOffset(TimeSpan.FromHours(8)),
            ToUtc: Time.ToOffset(TimeSpan.FromHours(8))))).Total);
        Assert.Equal(0, (await store.Repository.QueryChangesAsync(new(FromUtc: Time.AddHours(1).AddTicks(1)))).Total);
        Assert.Equal(0, (await store.Repository.QueryChangesAsync(new(ToUtc: Time.AddTicks(-1)))).Total);
        Assert.Equal(3, (await store.Repository.QueryChangesAsync(new("0", InventoryCategory.Software))).Total);
    }

    [Fact]
    public async Task DashboardSeparatesAttemptFromCompleteAndDoesNotReportFailedAsZero()
    {
        using var store = new TestStore(false);
        await store.Repository.CommitAsync(Run(0));
        await store.Repository.CommitAsync(Run(1, software: CollectionStatus.Partial));
        var dashboard = await store.Repository.GetDashboardAsync();
        Assert.Equal(Time.AddMinutes(1), dashboard.LastAttemptUtc);
        Assert.Equal(Time, dashboard.LastCompleteUtc);
        var software = Assert.Single(dashboard.Categories, x => x.Category == InventoryCategory.Software);
        Assert.Null(software.CurrentChanges);
        Assert.Equal(3, software.BaselineItems);
        Assert.Equal(Time, software.BaselineAtUtc);
        Assert.Equal(8, dashboard.RecentChanges.Count);
        Assert.Equal(1, (await store.Repository.QuerySnapshotsAsync(InventoryCategory.Software)).Total);
        Assert.Equal(2, (await store.Repository.QuerySnapshotsAsync(InventoryCategory.Network)).Total);
    }

    [Fact]
    public async Task SnapshotComparisonIsPagedValidatedAndDoesNotWriteHistory()
    {
        using var store = new TestStore(false);
        await store.Repository.CommitAsync(Run(0, 150));
        await store.Repository.CommitAsync(Run(1, 150));
        var headers = (await store.Repository.QuerySnapshotsAsync(InventoryCategory.Software)).Items;
        var before = headers[1]; var after = headers[0];
        Assert.Equal(150, after.ItemCount);
        var compared = await store.Repository.CompareSnapshotsAsync(before.Id, after.Id, page: 2);
        Assert.Equal(150, compared.Total);
        Assert.Equal(40, compared.Items.Count);
        Assert.All(compared.Items, x => Assert.Contains(x.Fields, f => f.Name == "version"));
        Assert.Equal(600, store.Scalar("SELECT COUNT(*) FROM changes"));
        Assert.Equal(4, store.Scalar("SELECT COUNT(*) FROM baselines"));
        Assert.Equal(2, store.Scalar("SELECT COUNT(*) FROM scan_runs"));
        await Assert.ThrowsAsync<ArgumentException>(() => store.Repository.CompareSnapshotsAsync(before.Id, before.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => store.Repository.CompareSnapshotsAsync(after.Id, before.Id));
        var network = (await store.Repository.QuerySnapshotsAsync(InventoryCategory.Network)).Items[0];
        await Assert.ThrowsAsync<ArgumentException>(() => store.Repository.CompareSnapshotsAsync(before.Id, network.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.Repository.CompareSnapshotsAsync(Guid.NewGuid(), after.Id));
    }

    [Fact]
    public async Task PreviewIsBoundedAndFullEvidencePreservesNullEmptyAndMissingFields()
    {
        using var store = new TestStore(false);
        ScanRun Describe(ScanRun run, bool changed) => new(run.Id, run.StartedAtUtc, run.CompletedAtUtc, false,
            run.Results.Select(r => new CollectionResult(r.Category, r.Status, r.StartedAtUtc, r.CompletedAtUtc,
                r.Items.Select(item => new SnapshotItem(item.StableKey, new Dictionary<string, FieldValue>
                {
                    ["display_name"] = new(new string('n', 400)),
                    ["command"] = new(new string(changed ? 'b' : 'a', 600)),
                    ["nullable"] = new(changed ? "" : null),
                    [changed ? "new_field" : "old_field"] = new("present")
                })).ToArray())).ToArray());
        await store.Repository.CommitAsync(Describe(Run(0, 1), false));
        await store.Repository.CommitAsync(Describe(Run(1, 1), true));
        var preview = (await store.Repository.QueryChangesAsync(new())).Items[0];
        Assert.Equal(201, preview.Label.Length);
        Assert.Equal(257, preview.After!.Value!.Length);
        var evidence = (await store.Repository.GetChangeAsync(preview.Id))!;
        Assert.Equal(600, evidence.After!.Fields["command"].Value!.Length);
        Assert.Null(evidence.Before!.Fields["nullable"].Value);
        Assert.Equal("", evidence.After.Fields["nullable"].Value);
        Assert.DoesNotContain("new_field", evidence.Before.Fields.Keys);
        Assert.Null(await store.Repository.GetChangeAsync("missing"));
    }

    [Fact]
    public async Task LegacyMigrationBackfillsQueriesAndRollsBackAllSchemaChangesOnFailure()
    {
        using var store = new TestStore(false);
        await store.Repository.CommitAsync(Run(0, 40));
        await store.Repository.CommitAsync(Run(1, 40));
        store.Execute("""
            DROP INDEX changes_category_observed;
            DROP INDEX snapshots_category_observed;
            ALTER TABLE changes DROP COLUMN search_text;
            ALTER TABLE changes DROP COLUMN summary_json;
            ALTER TABLE snapshots DROP COLUMN item_count;
            PRAGMA user_version=1;
            CREATE TRIGGER migration_failure BEFORE UPDATE ON changes BEGIN SELECT RAISE(ABORT,'migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => store.Repository.GetDashboardAsync());
        Assert.Equal(1, store.Scalar("PRAGMA user_version"));
        Assert.Equal(0, store.Scalar("SELECT COUNT(*) FROM pragma_table_info('changes') WHERE name='search_text'"));
        Assert.Equal(160, store.Scalar("SELECT COUNT(*) FROM changes"));
        store.Execute("DROP TRIGGER migration_failure;");
        var migrated = await store.Repository.QueryChangesAsync(new("%_';--"));
        Assert.Equal(4, migrated.Total);
        Assert.Equal(2, store.Scalar("PRAGMA user_version"));
        Assert.Equal(40, (await store.Repository.QuerySnapshotsAsync(InventoryCategory.Software)).Items[0].ItemCount);
        Assert.Equal(4, (await store.Repository.QueryChangesAsync(new("%_';--"))).Total);
    }

    [Fact]
    public async Task EmptyAndCancelledQueriesDoNotCreateAStore()
    {
        using var store = new TestStore(false);
        Assert.Equal(0, (await store.Repository.GetDashboardAsync()).TotalRuns);
        Assert.Empty((await store.Repository.QueryChangesAsync(new())).Items);
        Assert.Empty((await store.Repository.QuerySnapshotsAsync(InventoryCategory.Software)).Items);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.Repository.CompareSnapshotsAsync(Guid.NewGuid(), Guid.NewGuid()));
        using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Repository.QueryChangesAsync(new(), source.Token));
        Assert.False(System.IO.File.Exists(store.DatabasePath));
    }
}
