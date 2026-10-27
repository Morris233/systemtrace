using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using SystemTrace.App.Services;
using SystemTrace.Core;
using SystemTrace.Infrastructure;
using Xunit;

namespace SystemTrace.Tests;

public sealed class MonitoringTests
{
    [Fact]
    public async Task DefaultsAreOptInAndSettingsRoundTripAtomically()
    {
        using var store = new TestStore(false);
        var path = Path.ChangeExtension(store.DatabasePath, ".json");
        var preferences = new JsonPreferencesStore(path);
        var defaults = await preferences.LoadAsync();
        Assert.False(defaults.BackgroundEnabled); Assert.False(defaults.MinimizeToTray); Assert.False(defaults.StartAtLogin);
        Assert.Equal(30, defaults.IntervalMinutes);
        Assert.False(File.Exists(path));
        var enabled = defaults with { BackgroundEnabled = true, MinimizeToTray = true, IntervalMinutes = 5, Language = "zh-CN" };
        await preferences.SaveAsync(enabled);
        Assert.Equal(enabled, await new JsonPreferencesStore(path).LoadAsync());
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preferences.SaveAsync(defaults, cancellation.Token));
        Assert.Equal(enabled, await preferences.LoadAsync());
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!));
        await File.WriteAllTextAsync(path, """{"Version":99}""");
        await Assert.ThrowsAsync<ArgumentException>(() => preferences.LoadAsync());
        Assert.Contains("99", await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData(0, 90)]
    [InlineData(1441, 90)]
    [InlineData(30, 6)]
    [InlineData(30, 3651)]
    public void InvalidPreferencesAreRejected(int interval, int days) =>
        Assert.Throws<ArgumentException>(() => new AppPreferences(IntervalMinutes: interval, RetentionDays: days).Validate());

    [Fact]
    public void ScheduleIsOptInResetsOnPauseAndNeverCatchesUpInABurst()
    {
        var clock = HistoryReaderTests.Time;
        var schedule = new MonitorSchedule();
        schedule.Configure(false, false, 30, clock);
        Assert.False(schedule.TakeDue(clock.AddDays(1), true));
        schedule.Configure(true, false, 30, clock);
        Assert.False(schedule.TakeDue(clock.AddMinutes(29), true));
        Assert.True(schedule.TakeDue(clock.AddHours(8), true));
        Assert.False(schedule.TakeDue(clock.AddHours(8), true));
        schedule.Configure(true, true, 30, clock.AddHours(8));
        Assert.False(schedule.TakeDue(clock.AddDays(1), true));
        schedule.Configure(true, false, 5, clock);
        Assert.False(schedule.TakeDue(clock.AddMinutes(5), false)); // busy is skipped, never queued
        Assert.False(schedule.TakeDue(clock.AddMinutes(5), true));
        Assert.True(schedule.TakeDue(clock.AddMinutes(10), true));
        schedule.Configure(false, false, 5, clock);
        Assert.Null(schedule.NextDueUtc);
    }

    [Fact]
    public async Task ProgressCancellationAndScanGateProtectTheEntirePipeline()
    {
        using var store = new TestStore(false);
        var collector = new WaitingCollector();
        var session = new SystemScanSession(store.Repository, [collector, new CollectorTests.EmptyCollector(InventoryCategory.Startup),
            new CollectorTests.EmptyCollector(InventoryCategory.Drivers), new CollectorTests.EmptyCollector(InventoryCategory.Network)]);
        var reports = new List<ScanProgress>();
        using var cancellation = new CancellationTokenSource();
        var pending = session.RunAsync(new InlineProgress(reports.Add), cancellation.Token);
        await collector.Started.Task;
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.RunAsync());
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(0, (await store.Repository.LoadAsync()).TotalRuns);
        Assert.Equal(InventoryCategory.Software, Assert.Single(reports).Category);
        collector.Wait = false;
        await session.RunAsync(new InlineProgress(reports.Add));
        Assert.Equal(4, reports.Last().Completed);
        Assert.Equal(1, (await store.Repository.LoadAsync()).TotalRuns);
    }

    [Fact]
    public async Task RetentionKeepsBaselinesAndLatestFailedAttemptThenRecoveryDiffsLastSuccess()
    {
        using var store = new TestStore(false);
        await store.Repository.CommitAsync(HistoryReaderTests.Run(0));
        await store.Repository.CommitAsync(HistoryReaderTests.Run(1, software: CollectionStatus.Failed));
        await store.Repository.PruneAsync(HistoryReaderTests.Time.AddDays(1));
        var history = await store.Repository.LoadAsync();
        Assert.Equal(2, history.TotalRuns);
        Assert.Empty(history.Changes);
        Assert.Equal(4, history.Baselines.Count);
        Assert.Equal(CollectionStatus.Failed, history.LastAttempt!.Results[0].Status);
        Assert.Equal(4, store.Scalar("SELECT COUNT(*) FROM snapshots"));
        await store.Repository.CommitAsync(HistoryReaderTests.Run(2));
        var recovered = (await store.Repository.QueryChangesAsync(new(Category: InventoryCategory.Software))).Items;
        Assert.Equal(3, recovered.Count);
        Assert.All(recovered, change => Assert.Equal(HistoryReaderTests.Time, change.ObservedFromUtc));
    }

    [Fact]
    public async Task RetentionActuallyRemovesObsoleteRunsAndClearRequiresNewBaselines()
    {
        using var store = new TestStore(false);
        for (var step = 0; step < 4; step++) await store.Repository.CommitAsync(HistoryReaderTests.Run(step));
        await store.Repository.PruneAsync(HistoryReaderTests.Time.AddMinutes(2));
        Assert.Equal(2, store.Scalar("SELECT COUNT(*) FROM scan_runs"));
        Assert.Equal(8, store.Scalar("SELECT COUNT(*) FROM snapshots"));
        Assert.Equal(24, store.Scalar("SELECT COUNT(*) FROM changes"));
        await store.Repository.ClearAsync();
        var empty = await store.Repository.LoadAsync();
        Assert.Equal(0, empty.TotalRuns); Assert.Empty(empty.Baselines); Assert.Empty(empty.Changes);
        Assert.Equal(2, store.Scalar("PRAGMA user_version"));
        await store.Repository.CommitAsync(HistoryReaderTests.Run(5));
        Assert.Empty((await store.Repository.QueryChangesAsync(new())).Items);
    }

    [Fact]
    public async Task MaintenanceFailureRollsBackAndCancelledClearKeepsHistory()
    {
        using var store = new TestStore(false);
        await store.Repository.CommitAsync(HistoryReaderTests.Run(0));
        await store.Repository.CommitAsync(HistoryReaderTests.Run(1));
        store.Execute("CREATE TRIGGER retain_failure BEFORE DELETE ON snapshots BEGIN SELECT RAISE(ABORT,'test'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => store.Repository.ClearAsync());
        Assert.Equal(2, (await store.Repository.LoadAsync()).TotalRuns);
        Assert.Equal(12, store.Scalar("SELECT COUNT(*) FROM changes"));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Repository.ClearAsync(cancellation.Token));
        Assert.Equal(4, store.Scalar("SELECT COUNT(*) FROM baselines"));
    }

    [Fact]
    public async Task JsonExportIsFilteredCompleteAndPrivateByDefault()
    {
        using var store = new TestStore(false);
        await store.Repository.CommitAsync(HistoryReaderTests.Run(0, 65));
        await store.Repository.CommitAsync(HistoryReaderTests.Run(1, 65));
        using var output = new MemoryStream();
        await store.Repository.ExportAsync(output, ReportFormat.Json, new(Category: InventoryCategory.Software));
        var text = Encoding.UTF8.GetString(output.ToArray());
        using var document = JsonDocument.Parse(text);
        Assert.True(document.RootElement.GetProperty("redacted").GetBoolean());
        var changes = document.RootElement.GetProperty("changes");
        Assert.Equal(65, changes.GetArrayLength()); // export includes every page
        Assert.DoesNotContain("Application ", text);
        Assert.DoesNotContain("Caf", text);
        Assert.DoesNotContain("Software/", text);
        Assert.Contains("item-", text);
        var fields = changes[0].GetProperty("Fields").EnumerateArray().ToArray();
        var nullable = fields.Single(field => field.GetProperty("Name").GetString() == "nullable");
        Assert.Equal("null", nullable.GetProperty("Before").GetProperty("State").GetString());
        Assert.Equal("empty", nullable.GetProperty("After").GetProperty("State").GetString());
        using var raw = new MemoryStream();
        await store.Repository.ExportAsync(raw, ReportFormat.Json, new("%_';--", InventoryCategory.Software), false);
        using var rawDoc = JsonDocument.Parse(raw.ToArray());
        Assert.Single(rawDoc.RootElement.GetProperty("changes").EnumerateArray());
        Assert.Contains("Software/0", Encoding.UTF8.GetString(raw.ToArray()));
        Assert.Equal(260, store.Scalar("SELECT COUNT(*) FROM changes"));
    }

    [Fact]
    public async Task CsvQuotesNewlinesGuardsFormulasAndRedactsCommandsAndIdentities()
    {
        using var store = new TestStore(false);
        ScanRun Run(int step)
        {
            var template = HistoryReaderTests.Run(step, 0);
            var fields = new Dictionary<string, FieldValue>
            {
                ["display_name"] = new(" =HYPERLINK(\"bad\")\nline"),
                ["command"] = new(@"C:\Users\PrivateUser\secret.cmd --token=secret"),
                ["version"] = new(step.ToString()),
                ["driver_version"] = new("\0=HYPERLINK(\"private\")"),
                ["driver_date"] = new(@"C:\Users\PrivateUser\secret")
            };
            return new(template.Id, template.StartedAtUtc, template.CompletedAtUtc, false,
                template.Results.Select(result => new CollectionResult(result.Category, result.Status, result.StartedAtUtc,
                    result.CompletedAtUtc, [new SnapshotItem(@"C:\Users\PrivateUser\device-guid", fields)])).ToArray());
        }
        await store.Repository.CommitAsync(Run(0)); await store.Repository.CommitAsync(Run(1));
        using var raw = new MemoryStream();
        await store.Repository.ExportAsync(raw, ReportFormat.Csv, new(), false);
        var csv = Encoding.UTF8.GetString(raw.ToArray());
        Assert.Contains("\"' =HYPERLINK(\"\"bad\"\")\nline\"", csv);
        Assert.Equal((byte)0xef, raw.ToArray()[0]);
        Assert.Contains("\"'\0=HYPERLINK", csv);
        using var redacted = new MemoryStream();
        await store.Repository.ExportAsync(redacted, ReportFormat.Csv, new());
        var safe = Encoding.UTF8.GetString(redacted.ToArray());
        Assert.DoesNotContain("PrivateUser", safe); Assert.DoesNotContain("secret", safe); Assert.DoesNotContain("device-guid", safe);
        Assert.DoesNotContain("HYPERLINK", safe);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Repository.ExportAsync(new MemoryStream(), ReportFormat.Json, new(), token: cancellation.Token));
    }

    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public void LoginStartOnlyManagesItsMatchingIsolatedEntry()
    {
        var path = @"Software\SystemTrace.Tests." + Guid.NewGuid().ToString("N");
        var executable = Path.Combine(Path.GetTempPath(), "SystemTrace test", "SystemTrace.App.exe");
        var login = new WindowsLoginStart(executable, path);
        try
        {
            Assert.False(login.IsEnabled);
            login.SetEnabled(true); Assert.True(login.IsEnabled);
            using (var key = Registry.CurrentUser.OpenSubKey(path, true)!)
            {
                Assert.Equal("\"" + executable + "\" --login", key.GetValue("SystemTrace"));
                key.SetValue("OtherApp", "preserve");
            }
            login.SetEnabled(false); Assert.False(login.IsEnabled);
            using (var key = Registry.CurrentUser.OpenSubKey(path, true)!)
            {
                Assert.Equal("preserve", key.GetValue("OtherApp"));
                key.SetValue("SystemTrace", "foreign");
            }
            login.SetEnabled(false);
            Assert.Throws<InvalidOperationException>(() => login.SetEnabled(true));
            using var foreign = Registry.CurrentUser.OpenSubKey(path, false);
            Assert.Equal("foreign", foreign!.GetValue("SystemTrace"));
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
    }

    [Fact]
    public async Task StartupFailureRollsBackSavedPreferencesAndAppearanceNeverWritesLogin()
    {
        using var store = new TestStore(false);
        var preferences = new MemoryPreferences();
        var login = new FailingLogin();
        var service = new ApplicationServices(preferences, login, store.Repository, store.Repository, new NoAppearance(), new AppPreferences());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ApplyAsync(new AppPreferences(StartAtLogin: true), CancellationToken.None));
        Assert.False(service.Preferences.StartAtLogin);
        Assert.False(preferences.Saved.StartAtLogin);
        await service.SaveAppearanceAsync("zh-CN", false, CancellationToken.None);
        Assert.Equal(1, login.Calls);
        Assert.Equal("zh-CN", preferences.Saved.Language);
        Assert.False(preferences.Saved.IsDark);
    }

    [Fact]
    public void DiagnosticLoggingDoesNotWriteExceptionMessageOrEvidence()
    {
        var path = Path.Combine(Path.GetTempPath(), "SystemTrace.LogTests", Guid.NewGuid().ToString("N"));
        try
        {
            using var provider = new LocalLogProvider(path);
            var logger = provider.CreateLogger("test");
            Microsoft.Extensions.Logging.LoggerExtensions.LogError(logger, new IOException("secret command C:\\Users\\Private"), "private token {Token}", "never-save");
            var log = File.ReadAllText(Path.Combine(path, "diagnostics.log"));
            Assert.Contains("IOException", log);
            Assert.DoesNotContain("secret", log); Assert.DoesNotContain("private", log, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("never-save", log);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    private sealed class MemoryPreferences : IPreferencesStore
    {
        public AppPreferences Saved { get; private set; } = new();
        public Task<AppPreferences> LoadAsync(CancellationToken token = default) => Task.FromResult(Saved);
        public Task SaveAsync(AppPreferences preferences, CancellationToken token = default) { Saved = preferences; return Task.CompletedTask; }
    }
    private sealed class FailingLogin : ILoginStart
    {
        public int Calls { get; private set; }
        public bool IsEnabled => false;
        public void SetEnabled(bool enabled) { Calls++; throw new UnauthorizedAccessException(); }
    }
    private sealed class NoAppearance : IAppearanceService
    {
        public string Language => "en-US"; public bool IsDark => true; public string Text(string key) => key;
        public void ToggleLanguage() { }
        public void ToggleTheme() { }
    }

    private sealed class InlineProgress(Action<ScanProgress> report) : IProgress<ScanProgress>
    { public void Report(ScanProgress value) => report(value); }
    private sealed class WaitingCollector : ISystemCollector
    {
        public InventoryCategory Category => InventoryCategory.Software;
        public bool Wait { get; set; } = true;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<CollectionResult> CollectAsync(CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            if (Wait) await Task.Delay(Timeout.Infinite, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            return new(Category, CollectionStatus.Succeeded, now, now, []);
        }
    }
}
