using SystemTrace.Core;

namespace SystemTrace.Tests;

internal sealed class SampleSession(IScanRepository repository) : ISystemScanSession
{
    public Task<InventoryHistory> LoadAsync(CancellationToken cancellationToken = default) => repository.LoadAsync(cancellationToken);

    public async Task<InventoryHistory> RunAsync(CancellationToken cancellationToken = default)
    {
        var history = await repository.LoadAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        if (history.LastAttempt is { } last && now < last.CompletedAtUtc)
            throw new InvalidOperationException("System clock precedes the last observation");
        await repository.CommitAsync(CreateRun(history.TotalRuns, now), cancellationToken).ConfigureAwait(false);
        return await repository.LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    public static ScanRun CreateRun(int step, DateTimeOffset time)
    {
        var changed = step % 2 != 0;
        var data = new Dictionary<InventoryCategory, SnapshotItem[]>
        {
            [InventoryCategory.Software] = [Item("demo/software/editor", "SampleEditor", changed ? "1.1" : "1.0")],
            [InventoryCategory.Startup] = [Item(changed ? "demo/startup/b" : "demo/startup/a", "SampleStartup", "1")],
            [InventoryCategory.Drivers] = [Item("demo/driver/display", "SampleDriver", "1.0")],
            [InventoryCategory.Network] = [Item("demo/network/adapter", "SampleAdapter", changed ? "Online" : "Offline")]
        };
        return new ScanRun(Guid.NewGuid(), time, time, true,
            Enum.GetValues<InventoryCategory>().Select(category => new CollectionResult(category,
                CollectionStatus.Succeeded, time, time, data[category])).ToArray());
    }

    private static SnapshotItem Item(string key, string labelKey, string value) => new(key,
        new Dictionary<string, FieldValue>
        {
            ["label_key"] = new(labelKey),
            ["value"] = new(value)
        });
}
