using SystemTrace.Core;

namespace SystemTrace.Infrastructure;

public sealed class SystemScanSession : ISystemScanSession
{
    private readonly IScanRepository repository;
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

    public async Task<InventoryHistory> RunAsync(CancellationToken cancellationToken = default)
    {
        var history = await repository.LoadAsync(cancellationToken).ConfigureAwait(false);
        var started = DateTimeOffset.UtcNow;
        if (history.LastAttempt is { } last && started < last.CompletedAtUtc)
            throw new InvalidOperationException("System clock precedes the last observation");
        var results = new List<CollectionResult>();
        foreach (var collector in collectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
        cancellationToken.ThrowIfCancellationRequested();
        var run = new ScanRun(Guid.NewGuid(), started, DateTimeOffset.UtcNow, false, results);
        await repository.CommitAsync(run, cancellationToken).ConfigureAwait(false);
        return await repository.LoadAsync(cancellationToken).ConfigureAwait(false);
    }
}
