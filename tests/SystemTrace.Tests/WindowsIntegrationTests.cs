using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;
using SystemTrace.Collectors;
using SystemTrace.Core;
using SystemTrace.Infrastructure;
using Xunit;

namespace SystemTrace.Tests;

public sealed class WindowsIntegrationTests
{
    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public async Task ReadOnlyWindowsScanCreatesAvailableBaselinesWithoutInitialEvents()
    {
        using var store = new TestStore(isDemo: false);
        ISystemCollector[] collectors = [new SoftwareCollector(new WindowsRegistrySource()),
            new StartupCollector(new WindowsRegistrySource(), new WindowsStartupFileSource()),
            new DriverCollector(new WindowsDeviceSource()), new NetworkCollector(new WindowsNetworkSource())];
        var session = new SystemScanSession(store.Repository, collectors);
        var watch = Stopwatch.StartNew();
        var history = await session.RunAsync();
        Assert.Equal(1, history.TotalRuns);
        Assert.Empty(history.Changes);
        Assert.False(history.LastAttempt!.IsDemo);
        Assert.Equal(4, history.LastAttempt.Results.Count);
        Assert.Equal(history.LastAttempt.Results.Count(r => r.Status == CollectionStatus.Succeeded), history.Baselines.Count);
        Assert.InRange(history.Baselines.Count, 1, 4);
        Assert.Equal(CollectionStatus.Succeeded, history.LastAttempt.Results.Single(r => r.Category == InventoryCategory.Network).Status);
        using var identity = WindowsIdentity.GetCurrent();
        var elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        var summaryPath = Environment.GetEnvironmentVariable("SYSTEMTRACE_LIVE_SUMMARY");
        if (summaryPath is not null)
            await File.WriteAllTextAsync(summaryPath, JsonSerializer.Serialize(new
            {
                ElapsedMilliseconds = watch.ElapsedMilliseconds,
                Elevated = elevated,
                Baselines = history.Baselines.Count,
                Categories = history.LastAttempt.Results.Select(r => new
                {
                    Category = r.Category.ToString(),
                    Status = r.Status.ToString(),
                    Count = r.Items.Count,
                    r.ErrorCode,
                    r.SkippedItemCount,
                    r.Warnings
                })
            }, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public async Task IsolatedRegistrySoftwareAddModifyRemoveAndViewSharingWork()
    {
        var path = @"Software\SystemTrace.Tests." + Guid.NewGuid().ToString("N");
        var location = new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Registry64, path);
        using var store = new TestStore(isDemo: false);
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        try
        {
            using (hive.CreateSubKey(path)) { }
            var session = new SystemScanSession(store.Repository,
                [new SoftwareCollector(new WindowsRegistrySource(), [location]), new CollectorTests.EmptyCollector(InventoryCategory.Startup),
                 new CollectorTests.EmptyCollector(InventoryCategory.Drivers), new CollectorTests.EmptyCollector(InventoryCategory.Network)]);
            await session.RunAsync();
            using (var key = hive.CreateSubKey(path + @"\sample"))
            { key.SetValue("DisplayName", "Isolated sample"); key.SetValue("DisplayVersion", "1"); }
            var added = await session.RunAsync();
            Assert.Equal(ChangeKind.Added, Assert.Single(added.Changes).Kind);
            using (var otherView = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32))
            using (var same = otherView.OpenSubKey(path + @"\sample", writable: false))
                Assert.Equal("Isolated sample", same?.GetValue("DisplayName"));
            using (var key = hive.OpenSubKey(path + @"\sample", writable: true)!) key.SetValue("DisplayVersion", "2");
            var modified = await session.RunAsync();
            Assert.Contains(modified.Changes, c => c.Kind == ChangeKind.Modified && c.Fields.Any(f => f.Name == "version" && f.After?.Value == "2"));
            hive.DeleteSubKeyTree(path + @"\sample");
            Assert.Contains((await session.RunAsync()).Changes, c => c.Kind == ChangeKind.Removed);
        }
        finally { hive.DeleteSubKeyTree(path, throwOnMissingSubKey: false); }
    }

    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public async Task IsolatedStartupCommandsAndFileMetadataAreReadWithoutExecution()
    {
        var id = Guid.NewGuid().ToString("N");
        var registryPath = @"Software\SystemTrace.Tests." + id;
        var folder = Path.Combine(Path.GetTempPath(), "SystemTrace.StartupTests", id);
        var location = new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Registry64, registryPath);
        const string command = @"%TEMP%\test-only.exe --do-not-execute";
        Directory.CreateDirectory(folder);
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        try
        {
            using (var key = hive.CreateSubKey(registryPath)) key.SetValue("Sample", command, RegistryValueKind.ExpandString);
            await File.WriteAllTextAsync(Path.Combine(folder, "sample.cmd"), "This is inert fixture data, not a command.");
            var collector = new StartupCollector(new WindowsRegistrySource(), new WindowsStartupFileSource([("Test", folder)]), [location]);
            var result = await collector.CollectAsync();
            Assert.Equal(CollectionStatus.Succeeded, result.Status);
            Assert.Equal(2, result.Items.Count);
            Assert.Equal(command, result.Items.Single(i => i.Fields.ContainsKey("command")).Fields["command"].Value);
            Assert.Equal("sample.cmd", result.Items.Single(i => i.Fields.ContainsKey("relative_path")).Fields["relative_path"].Value);
            using (var key = hive.OpenSubKey(registryPath, writable: true)!) key.SetValue("InvalidCommandType", 42, RegistryValueKind.DWord);
            var partial = await collector.CollectAsync();
            Assert.Equal(CollectionStatus.Partial, partial.Status);
            Assert.Contains(partial.Items, item => item.Fields.GetValueOrDefault("command")?.ErrorCode == "UnsupportedRegistryType");
        }
        finally
        {
            hive.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false);
            Directory.Delete(folder, recursive: true);
        }
    }
}
