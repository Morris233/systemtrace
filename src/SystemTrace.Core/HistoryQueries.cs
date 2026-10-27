namespace SystemTrace.Core;

public sealed record ChangeQuery(string Text = "", InventoryCategory? Category = null,
    DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null, int Page = 0, int PageSize = 40)
{
    public void Validate()
    {
        if (Page < 0 || PageSize is < 1 or > 100 || Text.Length > 512)
            throw new ArgumentOutOfRangeException(nameof(Page));
        if (Category is { } category && !Enum.IsDefined(category)) throw new ArgumentOutOfRangeException(nameof(Category));
        if (FromUtc > ToUtc) throw new ArgumentException("Reversed observation range");
    }
}

public sealed record PageResult<T>(IReadOnlyList<T> Items, long Total, int Page, int PageSize)
{
    public int PageCount => (int)Math.Min(int.MaxValue, Math.Max(1, Math.Ceiling((double)Total / PageSize)));
}

public sealed record ChangePreview(string Id, InventoryCategory Category, ChangeKind Kind,
    DateTimeOffset ObservedFromUtc, DateTimeOffset ObservedToUtc, string Label,
    string FieldName, FieldValue? Before, FieldValue? After);
public sealed record SnapshotHeader(Guid Id, Guid ScanId, InventoryCategory Category, DateTimeOffset ObservedAtUtc, int ItemCount);
public sealed record CategoryOverview(InventoryCategory Category, CollectionStatus? Status,
    int? BaselineItems, DateTimeOffset? BaselineAtUtc, long TotalChanges, int? CurrentChanges,
    int SkippedItems, IReadOnlyList<string> Warnings, string? ErrorCode);
public sealed record DashboardSummary(int TotalRuns, DateTimeOffset? LastAttemptUtc, DateTimeOffset? LastCompleteUtc,
    IReadOnlyList<CategoryOverview> Categories, IReadOnlyList<ChangePreview> RecentChanges)
{
    public static DashboardSummary Empty { get; } = new(0, null, null,
        Array.AsReadOnly(Enum.GetValues<InventoryCategory>().Select(c => new CategoryOverview(c, null, null, null, 0, null, 0, [], null)).ToArray()), []);
}

public interface IHistoryReader
{
    Task<DashboardSummary> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<PageResult<ChangePreview>> QueryChangesAsync(ChangeQuery query, CancellationToken cancellationToken = default);
    Task<Change?> GetChangeAsync(string id, CancellationToken cancellationToken = default);
    Task<PageResult<SnapshotHeader>> QuerySnapshotsAsync(InventoryCategory category, int page = 0, int pageSize = 40, CancellationToken cancellationToken = default);
    Task<PageResult<Change>> CompareSnapshotsAsync(Guid beforeId, Guid afterId, int page = 0, int pageSize = 40, CancellationToken cancellationToken = default);
}

public static class ChangePreviews
{
    public static ChangePreview Create(Change change)
    {
        var item = change.After ?? change.Before!;
        var label = item.Fields.GetValueOrDefault("display_name")?.Value ?? "";
        var priority = new[] { "version", "driver_version", "operational_status", "command", "display_name", "value" };
        var name = priority.FirstOrDefault(key => change.Fields.Any(field => field.Name == key)) ?? change.Fields.FirstOrDefault()?.Name ?? "";
        FieldValue? Preview(SnapshotItem? entry)
        {
            var field = entry?.Fields.GetValueOrDefault(name);
            return field is null ? null : field with { Value = Clip(field.Value, 256) };
        }
        return new ChangePreview(change.Id, change.Category, change.Kind, change.ObservedFromUtc, change.ObservedToUtc,
            Clip(label, 200)!, name, Preview(change.Before), Preview(change.After));
    }

    private static string? Clip(string? value, int limit) => value?.Length > limit ? value[..limit] + "…" : value;
}
