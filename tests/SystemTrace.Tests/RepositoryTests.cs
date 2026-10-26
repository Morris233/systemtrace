using Microsoft.Data.Sqlite;
using SystemTrace.Core;
using SystemTrace.Infrastructure;
using Xunit;

namespace SystemTrace.Tests;

public sealed class RepositoryTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-10-26T00:00:00Z");
    private static ScanRun Run(int step, Guid? id = null, CollectionStatus software = CollectionStatus.Succeeded) =>
        CreateRun(step, id, new Dictionary<InventoryCategory, CollectionStatus> { [InventoryCategory.Software] = software });

    private static ScanRun CreateRun(int step, Guid? id, Dictionary<InventoryCategory, CollectionStatus> statuses)
    {
        var demo = SampleSession.CreateRun(step, Time.AddMinutes(step));
        return new ScanRun(id ?? demo.Id, demo.StartedAtUtc, demo.CompletedAtUtc, true,
            demo.Results.Select(r => new CollectionResult(r.Category, statuses.GetValueOrDefault(r.Category, r.Status),
                r.StartedAtUtc, r.CompletedAtUtc,
                statuses.GetValueOrDefault(r.Category, r.Status) is CollectionStatus.Failed or CollectionStatus.Skipped ? [] : r.Items,
                statuses.GetValueOrDefault(r.Category, r.Status) == CollectionStatus.Succeeded ? null : "Simulated")).ToArray());
    }

    [Fact]
    public async Task EmptyLoadDoesNotCreateDatabaseAndBaselineSurvivesRestart()
    {
        using var store = new TestStore();
        Assert.Equal(0, (await store.Repository.LoadAsync()).TotalRuns);
        Assert.False(System.IO.File.Exists(store.DatabasePath));
        var committed = await store.Repository.CommitAsync(Run(0));
        Assert.Equal(0, committed.ChangeCount);
        var history = await new SqliteScanRepository(store.DatabasePath, true).LoadAsync();
        Assert.Equal(1, history.TotalRuns);
        Assert.Equal(4, history.Baselines.Count);
        Assert.Empty(history.Changes);
        Assert.Equal(2, store.Scalar("PRAGMA user_version"));
        Assert.Equal(4, store.Scalar("SELECT COUNT(*) FROM collection_results"));
    }

    [Fact]
    public async Task ChangedScanPersistsEvidenceAndReentryIsIdempotent()
    {
        using var store = new TestStore();
        await store.Repository.CommitAsync(Run(0));
        var run = Run(1);
        Assert.Equal(4, (await store.Repository.CommitAsync(run)).ChangeCount);
        Assert.True((await store.Repository.CommitAsync(run)).AlreadyCommitted);
        var history = await store.Repository.LoadAsync();
        Assert.Equal(2, history.TotalRuns);
        Assert.Equal(4, history.Changes.Count);
        Assert.All(history.Changes, c => Assert.Equal(Time, c.ObservedFromUtc));
        Assert.Equal(2, history.ChangeCounts[InventoryCategory.Startup]);
        Assert.Equal(0, history.ChangeCounts[InventoryCategory.Drivers]);
        Assert.Equal(0, (await store.Repository.CommitAsync(Run(3))).ChangeCount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Repository.CommitAsync(Run(2, run.Id)));
        Assert.Equal(3, (await store.Repository.LoadAsync()).TotalRuns);
    }

    [Theory]
    [InlineData(CollectionStatus.Partial)]
    [InlineData(CollectionStatus.Failed)]
    [InlineData(CollectionStatus.Skipped)]
    public async Task UnsuccessfulCategoryKeepsItsOwnBaselineAndRecoveryUsesLastSuccess(CollectionStatus status)
    {
        using var store = new TestStore();
        await store.Repository.CommitAsync(Run(0));
        var initial = (await store.Repository.LoadAsync()).Baselines.Single(b => b.Category == InventoryCategory.Software);
        await store.Repository.CommitAsync(Run(1, software: status));
        var history = await store.Repository.LoadAsync();
        Assert.Equal(status, history.LastAttempt!.Results.Single(r => r.Category == InventoryCategory.Software).Status);
        Assert.Equal(initial.Id, history.Baselines.Single(b => b.Category == InventoryCategory.Software).Id);
        Assert.DoesNotContain(history.Changes, c => c.Category == InventoryCategory.Software);
        Assert.Equal(3, history.Changes.Count);
        await store.Repository.CommitAsync(Run(3));
        var recovered = Assert.Single((await store.Repository.LoadAsync()).Changes, c => c.Category == InventoryCategory.Software);
        Assert.Equal(Time, recovered.ObservedFromUtc);
        Assert.Equal(Time.AddMinutes(3), recovered.ObservedToUtc);
    }

    [Fact]
    public async Task FirstPartialDoesNotBecomeAnEmptyBaseline()
    {
        using var store = new TestStore();
        await store.Repository.CommitAsync(Run(0, software: CollectionStatus.Partial));
        Assert.Equal(3, (await store.Repository.LoadAsync()).Baselines.Count);
        await store.Repository.CommitAsync(Run(1));
        Assert.DoesNotContain((await store.Repository.LoadAsync()).Changes, c => c.Category == InventoryCategory.Software);
    }

    [Fact]
    public async Task SuccessfulEmptyResultRemovesItemsAndPartialPreservesUnreadableEvidence()
    {
        using var store = new TestStore();
        await store.Repository.CommitAsync(Run(0));
        var attempted = Run(1);
        var unreadable = new SnapshotItem("demo/software/editor", new Dictionary<string, FieldValue>
        {
            ["value"] = new(null, "AccessDenied")
        });
        var partial = attempted.Results.Select(r => r.Category == InventoryCategory.Software
            ? new CollectionResult(r.Category, CollectionStatus.Partial, r.StartedAtUtc, r.CompletedAtUtc, [unreadable]) : r).ToArray();
        await store.Repository.CommitAsync(new ScanRun(attempted.Id, attempted.StartedAtUtc, attempted.CompletedAtUtc, true, partial));
        var saved = (await store.Repository.LoadAsync()).LastAttempt!.Results[0].Items[0].Fields["value"];
        Assert.Null(saved.Value);
        Assert.Equal("AccessDenied", saved.ErrorCode);
        var next = Run(2);
        var empty = next.Results.Select(r => r.Category == InventoryCategory.Software
            ? new CollectionResult(r.Category, CollectionStatus.Succeeded, r.StartedAtUtc, r.CompletedAtUtc, []) : r).ToArray();
        await store.Repository.CommitAsync(new ScanRun(next.Id, next.StartedAtUtc, next.CompletedAtUtc, true, empty));
        var removed = Assert.Single((await store.Repository.LoadAsync()).Changes, c => c.Category == InventoryCategory.Software);
        Assert.Equal(ChangeKind.Removed, removed.Kind);
        Assert.Equal(Time, removed.ObservedFromUtc);
    }

    [Fact]
    public async Task CanonicalRunHashIgnoresItemAndFieldEnumerationOrder()
    {
        using var store = new TestStore();
        var run = Run(0);
        await store.Repository.CommitAsync(run);
        var reversed = run.Results.Reverse().Select(r => new CollectionResult(r.Category, r.Status,
            r.StartedAtUtc, r.CompletedAtUtc, r.Items.Reverse().Select(item => new SnapshotItem(item.StableKey,
                item.Fields.Reverse().ToDictionary(f => f.Key, f => f.Value))).ToArray(), r.ErrorCode)).ToArray();
        Assert.True((await store.Repository.CommitAsync(new ScanRun(run.Id, run.StartedAtUtc, run.CompletedAtUtc, true, reversed))).AlreadyCommitted);
        Assert.Equal(1, (await store.Repository.LoadAsync()).TotalRuns);
    }

    [Fact]
    public async Task AllFourOutcomesRemainIndependentInOneRun()
    {
        using var store = new TestStore();
        await store.Repository.CommitAsync(Run(0));
        await store.Repository.CommitAsync(CreateRun(1, null, new()
        {
            [InventoryCategory.Software] = CollectionStatus.Succeeded,
            [InventoryCategory.Startup] = CollectionStatus.Partial,
            [InventoryCategory.Drivers] = CollectionStatus.Failed,
            [InventoryCategory.Network] = CollectionStatus.Skipped
        }));
        var history = await store.Repository.LoadAsync();
        Assert.Single(history.Changes);
        Assert.Equal(InventoryCategory.Software, history.Changes[0].Category);
        Assert.Equal(3, history.Baselines.Count(b => b.ObservedAtUtc == Time));
    }

    [Fact]
    public async Task SqlFailureAfterAnEarlierCategoryWriteRollsBackWholeRun()
    {
        using var store = new TestStore();
        await store.Repository.CommitAsync(Run(0));
        var baselineIds = (await store.Repository.LoadAsync()).Baselines.Select(b => b.Id).ToArray();
        store.Execute("CREATE TRIGGER injected_failure BEFORE INSERT ON snapshots WHEN NEW.category=1 BEGIN SELECT RAISE(ABORT,'test rollback'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => store.Repository.CommitAsync(Run(1)));
        var history = await store.Repository.LoadAsync();
        Assert.Equal(1, history.TotalRuns);
        Assert.Empty(history.Changes);
        Assert.Equal(baselineIds, history.Baselines.Select(b => b.Id));
        Assert.Equal(4, store.Scalar("SELECT COUNT(*) FROM collection_results"));
        Assert.Equal(4, store.Scalar("SELECT COUNT(*) FROM snapshots"));
    }

    [Fact]
    public async Task ConcurrentDuplicateSubmissionsCommitOnlyOnce()
    {
        using var store = new TestStore();
        await store.Repository.CommitAsync(Run(0));
        var scan = Run(1);
        var other = new SqliteScanRepository(store.DatabasePath, true);
        var results = await Task.WhenAll(store.Repository.CommitAsync(scan), other.CommitAsync(scan));
        Assert.Single(results, r => r.AlreadyCommitted);
        Assert.Equal(2, (await store.Repository.LoadAsync()).TotalRuns);
        Assert.Equal(4, store.Scalar("SELECT COUNT(*) FROM changes"));
    }

    [Fact]
    public async Task NewSchemaAndOriginMismatchAreRejectedWithoutOverwriting()
    {
        using var store = new TestStore();
        await store.Repository.CommitAsync(Run(0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SqliteScanRepository(store.DatabasePath, false).LoadAsync());
        store.Execute("PRAGMA user_version=99");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Repository.CommitAsync(Run(1)));
        Assert.Equal(99, store.Scalar("PRAGMA user_version"));
        Assert.Equal(1, store.Scalar("SELECT COUNT(*) FROM scan_runs"));
    }

    [Fact]
    public async Task UnversionedForeignDatabaseIsRejectedAndPreserved()
    {
        using var store = new TestStore();
        store.Execute("CREATE TABLE sqliteUserNotes(note TEXT); INSERT INTO sqliteUserNotes VALUES('preserve');");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Repository.CommitAsync(Run(0)));
        Assert.Equal(0, store.Scalar("PRAGMA user_version"));
        Assert.Equal(1, store.Scalar("SELECT COUNT(*) FROM sqliteUserNotes"));
        Assert.Equal(0, store.Scalar("SELECT COUNT(*) FROM sqlite_master WHERE name='scan_runs'"));
    }

    [Fact]
    public async Task CancelledRunDoesNotCreateOrModifyHistory()
    {
        using var store = new TestStore();
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Repository.CommitAsync(Run(0), source.Token));
        Assert.False(System.IO.File.Exists(store.DatabasePath));
        await store.Repository.CommitAsync(Run(0));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Repository.CommitAsync(Run(1), source.Token));
        Assert.Equal(1, (await store.Repository.LoadAsync()).TotalRuns);
    }

    [Fact]
    public async Task BackwardsObservationRollsBackAndSqlTextIsOnlyData()
    {
        using var store = new TestStore();
        await store.Repository.CommitAsync(Run(1));
        await Assert.ThrowsAsync<ArgumentException>(() => store.Repository.CommitAsync(Run(0)));
        var run = Run(2);
        var results = run.Results.Select(r => r.Category == InventoryCategory.Software
            ? new CollectionResult(r.Category, r.Status, r.StartedAtUtc, r.CompletedAtUtc,
                [InventoryTests.Item("'; DROP TABLE scan_runs; --", "a'\"; SELECT")]) : r).ToArray();
        await store.Repository.CommitAsync(new ScanRun(run.Id, run.StartedAtUtc, run.CompletedAtUtc, true, results));
        Assert.Equal(2, store.Scalar("SELECT COUNT(*) FROM scan_runs"));
        Assert.Contains((await store.Repository.LoadAsync()).Changes, c => c.StableKey.Contains("DROP TABLE", StringComparison.Ordinal));
    }
}
