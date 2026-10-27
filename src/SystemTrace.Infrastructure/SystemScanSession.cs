using SystemTrace.Core;

namespace SystemTrace.Infrastructure;

public sealed class SystemScanSession : IProgressScanSession
{
    private readonly IScanRepository repository;
    private int scanning;
    private readonly IReadOnlyList<ISystemCollector> collectors;

    public SystemScanSession(IScanRepository repository, IEnumerable<ISystemCollector> collectors)
    {
        this.repository = repository;
        var ordered = collectors.OrderBy(c => c.Category).ToArray();
        if (!ordered.Select(c => c.Category).SequenceEqual(Enum.GetValues<InventoryCategory>()))
            throw new ArgumentException("Exactly four unique collectors are required", nameof(collectors));
        this.collectors = Array.AsReadOnly(ordered);
    }

    public Task<InventoryHistory> LoadAsync(CancellationToken cancellationToken = default) => repository.LoadAsync(cancellationToken);

    public Task<InventoryHistory> RunAsync(CancellationToken cancellationToken = default) => RunAsync(null, cancellationToken);
    public async Task<InventoryHistory> RunAsync(IProgress<ScanProgress>? progress, CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref scanning, 1, 0) != 0) throw new InvalidOperationException("Scan already running");
        try
        {
            var history = await repository.LoadAsync(cancellationToken).ConfigureAwait(false);
            var started = DateTimeOffset.UtcNow;
            if (history.LastAttempt is { } last && started < last.CompletedAtUtc)
                throw new InvalidOperationException("System clock precedes the last observation");
            var results = new List<CollectionResult>();
            foreach (var collector in collectors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new ScanProgress(results.Count, collector.Category));
                var attempt = DateTimeOffset.UtcNow;
                try
                {
                    var result = await collector.CollectAsync(cancellationToken).ConfigureAwait(false);
                    if (result.Category != collector.Category) throw new InvalidOperationException("Collector contract violation");
                    results.Add(result);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    // A failed category is evidence of an unsuccessful attempt, never an empty inventory.
                    results.Add(new CollectionResult(collector.Category, CollectionStatus.Failed,
                        attempt, DateTimeOffset.UtcNow, [], "CollectorFailed"));
                }
            }
            progress?.Report(new ScanProgress(4, null));
            cancellationToken.ThrowIfCancellationRequested();
            var run = new ScanRun(Guid.NewGuid(), started, DateTimeOffset.UtcNow, false, results);
            await repository.CommitAsync(run, cancellationToken).ConfigureAwait(false);
            return await repository.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { Volatile.Write(ref scanning, 0); }
    }
}
