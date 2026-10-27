using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SystemTrace.App.Services;
using SystemTrace.Core;
using Xunit;

namespace SystemTrace.Tests;

public sealed class HardeningTests
{
    [Fact]
    public void SecondaryInstanceSignalsTheOwnerWithoutTakingItsLock()
    {
        var identity = "SystemTrace.Tests." + Guid.NewGuid().ToString("N");
        using var owner = new SingleInstance(identity);
        Assert.True(owner.IsOwner);
        using var activated = new ManualResetEventSlim();
        owner.Listen(() => activated.Set());
        using var second = new SingleInstance(identity);
        Assert.False(second.IsOwner);
        Assert.True(activated.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task CorruptDatabaseIsPreservedAndNeverAutomaticallyReset()
    {
        using var store = new TestStore(false);
        Directory.CreateDirectory(Path.GetDirectoryName(store.DatabasePath)!);
        var evidence = Encoding.UTF8.GetBytes("Unrecognized database fixture " + Guid.NewGuid());
        await File.WriteAllBytesAsync(store.DatabasePath, evidence);
        await Assert.ThrowsAsync<SqliteException>(() => store.Repository.GetDashboardAsync());
        Assert.Equal(evidence, await File.ReadAllBytesAsync(store.DatabasePath));
        await Assert.ThrowsAsync<SqliteException>(() => store.Repository.ClearAsync());
        Assert.Equal(evidence, await File.ReadAllBytesAsync(store.DatabasePath));
    }

    [Fact]
    public async Task EmptyMaintenanceCreatesNoFileAndClockRollbackCannotAdvanceBaselines()
    {
        using var store = new TestStore(false);
        await store.Repository.ClearAsync();
        await store.Repository.PruneAsync(DateTimeOffset.UtcNow);
        Assert.False(File.Exists(store.DatabasePath));
        var template = HistoryReaderTests.Run(0);
        var future = DateTimeOffset.UtcNow.AddYears(1);
        await store.Repository.CommitAsync(new ScanRun(template.Id, future, future, false,
            template.Results.Select(result => new CollectionResult(result.Category, result.Status, future, future, result.Items)).ToArray()));
        var session = new SystemTrace.Infrastructure.SystemScanSession(store.Repository,
            Enum.GetValues<InventoryCategory>().Select(category => new CollectorTests.EmptyCollector(category)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.RunAsync());
        Assert.Equal(1, (await store.Repository.GetDashboardAsync()).TotalRuns);
        Assert.Empty((await store.Repository.QueryChangesAsync(new())).Items);
    }

    [Fact]
    public async Task TwelveThousandChangesStayPagedAndStreamExport()
    {
        using var store = new TestStore(false);
        var watch = Stopwatch.StartNew();
        for (var step = 0; step < 5; step++) await store.Repository.CommitAsync(HistoryReaderTests.Run(step, 750));
        var fixtureMilliseconds = watch.ElapsedMilliseconds;
        watch.Restart();
        var first = await store.Repository.QueryChangesAsync(new());
        var filtered = await store.Repository.QueryChangesAsync(new("Application", InventoryCategory.Software));
        var queryMilliseconds = watch.ElapsedMilliseconds;
        Assert.Equal(12000, first.Total);
        Assert.Equal(40, first.Items.Count);
        Assert.Equal(2996, filtered.Total);
        watch.Restart();
        await store.Repository.ExportAsync(Stream.Null, ReportFormat.Json, new(Category: InventoryCategory.Software));
        var exportMilliseconds = watch.ElapsedMilliseconds;
        var summary = Environment.GetEnvironmentVariable("SYSTEMTRACE_PERF_SUMMARY");
        if (summary is not null)
            await File.WriteAllTextAsync(summary, JsonSerializer.Serialize(new
            {
                changes = first.Total,
                pageSize = first.Items.Count,
                fixtureMilliseconds,
                queryMilliseconds,
                exportMilliseconds
            }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
