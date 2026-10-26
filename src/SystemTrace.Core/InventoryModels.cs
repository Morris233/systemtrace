using System.Collections.ObjectModel;
using System.Text;

namespace SystemTrace.Core;

public enum InventoryCategory { Software, Startup, Drivers, Network }
public enum CollectionStatus { Succeeded, Partial, Failed, Skipped }
public enum ChangeKind { Added, Removed, Modified }
public sealed record FieldValue(string? Value, string? ErrorCode = null);

public sealed class SnapshotItem
{
    public string StableKey { get; }
    public IReadOnlyDictionary<string, FieldValue> Fields { get; }

    public SnapshotItem(string stableKey, IReadOnlyDictionary<string, FieldValue> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableKey);
        ArgumentNullException.ThrowIfNull(fields);
        StableKey = stableKey.Normalize(NormalizationForm.FormC);
        var copy = new SortedDictionary<string, FieldValue>(StringComparer.Ordinal);
        foreach (var (name, value) in fields)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(value);
            copy.Add(name.Normalize(NormalizationForm.FormC), value);
        }
        Fields = new ReadOnlyDictionary<string, FieldValue>(copy);
    }
}

public sealed class CollectionResult
{
    public InventoryCategory Category { get; }
    public CollectionStatus Status { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public DateTimeOffset CompletedAtUtc { get; }
    public IReadOnlyList<SnapshotItem> Items { get; }
    public string? ErrorCode { get; }
    public int SkippedItemCount { get; }
    public IReadOnlyList<string> Warnings { get; }

    public CollectionResult(InventoryCategory category, CollectionStatus status,
        DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc,
        IReadOnlyList<SnapshotItem> items, string? errorCode = null,
        int skippedItemCount = 0, IReadOnlyList<string>? warnings = null)
    {
        if (!Enum.IsDefined(category) || !Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(category));
        ArgumentNullException.ThrowIfNull(items);
        if (skippedItemCount < 0) throw new ArgumentOutOfRangeException(nameof(skippedItemCount));
        if (completedAtUtc < startedAtUtc) throw new ArgumentException("Collection interval is reversed");
        var copy = items.OrderBy(item => item.StableKey, StringComparer.Ordinal).ToArray();
        if (copy.Select(item => item.StableKey).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("Duplicate stable keys", nameof(items));
        if ((status is CollectionStatus.Failed or CollectionStatus.Skipped) && copy.Length != 0)
            throw new ArgumentException("Failed/skipped results cannot contain inventory", nameof(items));
        if (status == CollectionStatus.Succeeded && (errorCode is not null || copy.Any(item => item.Fields.Values.Any(f => f.ErrorCode is not null))))
            throw new ArgumentException("Unreadable fields require a partial outcome", nameof(status));
        Category = category;
        Status = status;
        StartedAtUtc = startedAtUtc.ToUniversalTime();
        CompletedAtUtc = completedAtUtc.ToUniversalTime();
        Items = Array.AsReadOnly(copy);
        ErrorCode = errorCode;
        SkippedItemCount = skippedItemCount;
        Warnings = Array.AsReadOnly((warnings ?? []).Distinct(StringComparer.Ordinal).ToArray());
    }
}

public sealed class ScanRun
{
    public Guid Id { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public DateTimeOffset CompletedAtUtc { get; }
    public bool IsDemo { get; }
    public IReadOnlyList<CollectionResult> Results { get; }

    public ScanRun(Guid id, DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc,
        bool isDemo, IReadOnlyList<CollectionResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (id == Guid.Empty || completedAtUtc < startedAtUtc) throw new ArgumentException("Invalid scan identity or interval");
        var copy = results.OrderBy(result => result.Category).ToArray();
        if (!copy.Select(result => result.Category).SequenceEqual(Enum.GetValues<InventoryCategory>()))
            throw new ArgumentException("A run requires exactly four unique category outcomes", nameof(results));
        if (copy.Any(result => result.StartedAtUtc < startedAtUtc || result.CompletedAtUtc > completedAtUtc))
            throw new ArgumentException("Collection interval must fit the run", nameof(results));
        Id = id;
        StartedAtUtc = startedAtUtc.ToUniversalTime();
        CompletedAtUtc = completedAtUtc.ToUniversalTime();
        IsDemo = isDemo;
        Results = Array.AsReadOnly(copy);
    }
}

public sealed class Snapshot
{
    public Guid Id { get; }
    public Guid ScanId { get; }
    public InventoryCategory Category { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public IReadOnlyList<SnapshotItem> Items { get; }

    public Snapshot(Guid id, Guid scanId, InventoryCategory category, DateTimeOffset observedAtUtc, IReadOnlyList<SnapshotItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (id == Guid.Empty || scanId == Guid.Empty || !Enum.IsDefined(category)) throw new ArgumentException("Invalid snapshot identity");
        var copy = items.OrderBy(i => i.StableKey, StringComparer.Ordinal).ToArray();
        if (copy.Select(i => i.StableKey).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("Duplicate snapshot keys", nameof(items));
        if (copy.Any(i => i.Fields.Values.Any(f => f.ErrorCode is not null)))
            throw new ArgumentException("Unreadable fields cannot become a successful snapshot", nameof(items));
        Id = id;
        ScanId = scanId;
        Category = category;
        ObservedAtUtc = observedAtUtc.ToUniversalTime();
        Items = Array.AsReadOnly(copy);
    }
}
public sealed record FieldDelta(string Name, FieldValue? Before, FieldValue? After);
public sealed record Change(string Id, InventoryCategory Category, string StableKey, ChangeKind Kind,
    DateTimeOffset ObservedFromUtc, DateTimeOffset ObservedToUtc,
    SnapshotItem? Before, SnapshotItem? After, IReadOnlyList<FieldDelta> Fields);
public sealed record ScanCommit(bool AlreadyCommitted, int ChangeCount);
public sealed record InventoryHistory(int TotalRuns, ScanRun? LastAttempt,
    IReadOnlyList<Snapshot> Baselines, IReadOnlyList<Change> Changes,
    IReadOnlyDictionary<InventoryCategory, int> ChangeCounts)
{
    public static InventoryHistory Empty { get; } = new(0, null, Array.Empty<Snapshot>(), Array.Empty<Change>(),
        new ReadOnlyDictionary<InventoryCategory, int>(Enum.GetValues<InventoryCategory>().ToDictionary(c => c, _ => 0)));
}

public interface IScanRepository
{
    Task<ScanCommit> CommitAsync(ScanRun run, CancellationToken cancellationToken = default);
    Task<InventoryHistory> LoadAsync(CancellationToken cancellationToken = default);
}

