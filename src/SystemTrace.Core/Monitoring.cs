namespace SystemTrace.Core;

public sealed record AppPreferences(int Version = 1, string? Language = null, bool IsDark = true,
    bool BackgroundEnabled = false, bool MinimizeToTray = false, bool StartAtLogin = false,
    int IntervalMinutes = 30, int RetentionDays = 90)
{
    public void Validate()
    {
        if (Version != 1 || IntervalMinutes is < 5 or > 1440 || RetentionDays != 0 && RetentionDays is < 7 or > 3650
            || Language is not null and not "en-US" and not "zh-CN")
            throw new ArgumentException("Invalid preferences");
        if (MinimizeToTray && !BackgroundEnabled) throw new ArgumentException("Tray requires background opt-in");
    }
}
public interface IPreferencesStore
{
    Task<AppPreferences> LoadAsync(CancellationToken token = default);
    Task SaveAsync(AppPreferences preferences, CancellationToken token = default);
}
public interface ILoginStart
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}
public sealed record ScanProgress(int Completed, InventoryCategory? Category, CollectionStatus? Status = null);
public interface IProgressScanSession : ISystemScanSession
{
    Task<InventoryHistory> RunAsync(IProgress<ScanProgress>? progress, CancellationToken cancellationToken = default);
}
public interface IHistoryMaintenance
{
    Task PruneAsync(DateTimeOffset cutoffUtc, CancellationToken token = default);
    Task ClearAsync(CancellationToken token = default);
}
public enum ReportFormat { Json, Csv }
public interface IReportExporter
{
    Task ExportAsync(Stream destination, ReportFormat format, ChangeQuery query, bool redact = true, CancellationToken token = default);
}
