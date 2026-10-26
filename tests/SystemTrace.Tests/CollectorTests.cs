using Microsoft.Win32;
using SystemTrace.Collectors;
using SystemTrace.Core;
using SystemTrace.Infrastructure;
using Xunit;

namespace SystemTrace.Tests;

public sealed class CollectorTests
{
    [Fact]
    public async Task SoftwareUsesSeparateMachineViewsAndOneUserViewWithoutSensitiveFields()
    {
        var source = new FakeRegistry { Subkeys = _ => SourceBatch<RegistryEntry>.Complete(Software("Same name")) };
        var result = await new SoftwareCollector(source).CollectAsync();
        Assert.Equal(CollectionStatus.Succeeded, result.Status);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(3, result.Items.Select(i => i.StableKey).Distinct().Count());
        Assert.Single(SoftwareCollector.DefaultLocations, l => l.Hive == RegistryHive.CurrentUser);
        Assert.All(source.RequestedFields, fields =>
        {
            Assert.DoesNotContain("UninstallString", fields);
            Assert.DoesNotContain("InstallLocation", fields);
        });
    }

    [Fact]
    public async Task MissingDisplayNamesAreCountedAndAccessErrorsKeepPartialEvidence()
    {
        var source = new FakeRegistry
        {
            Subkeys = location => location.View == RegistryView.Registry32
                ? new SourceBatch<RegistryEntry>([], ["AccessDenied"])
                : SourceBatch<RegistryEntry>.Complete(Software("Named"), new RegistryEntry("hidden", new Dictionary<string, FieldValue>()))
        };
        var result = await new SoftwareCollector(source).CollectAsync();
        Assert.Equal(CollectionStatus.Partial, result.Status);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.SkippedItemCount);
        Assert.Contains("AccessDenied", result.Warnings);
    }

    [Fact]
    public async Task StartupPreservesCommandsAndScopesWithoutExpandingOrExecutingThem()
    {
        const string command = @"%TEMP%\test-only.exe --must-not-run";
        var source = new FakeRegistry { Values = _ => SourceBatch<RegistryValueEntry>.Complete(new RegistryValueEntry("Example", new(command))) };
        var files = new FakeFiles { Batch = SourceBatch<StartupFileEntry>.Complete(new StartupFileEntry("CurrentUser", "sample.cmd", DateTimeOffset.UtcNow)) };
        var result = await new StartupCollector(source, files).CollectAsync();
        Assert.Equal(CollectionStatus.Succeeded, result.Status);
        Assert.Equal(7, result.Items.Count);
        Assert.Equal(7, result.Items.Select(i => i.StableKey).Distinct().Count());
        Assert.Equal(3, result.Items.Count(i => i.Fields["source"].Value == "RunOnce"));
        Assert.All(result.Items.Where(i => i.Fields.ContainsKey("command")), i => Assert.Equal(command, i.Fields["command"].Value));
        Assert.Contains("RunOnceTransient", result.Warnings);
    }

    [Fact]
    public async Task DeviceNullIsDistinctFromUnreadableAndEnumerationFailureIsNotEmptySuccess()
    {
        var source = new FakeDevices { Batch = SourceBatch<DeviceEntry>.Complete(Device(null)) };
        var collector = new DriverCollector(source);
        var absent = await collector.CollectAsync();
        Assert.Equal(CollectionStatus.Succeeded, absent.Status);
        Assert.Null(absent.Items[0].Fields["driver_version"].Value);
        source.Batch = SourceBatch<DeviceEntry>.Complete(Device(null, "AccessDenied"));
        Assert.Equal(CollectionStatus.Partial, (await collector.CollectAsync()).Status);
        source.Failure = new InventorySourceException("CM:0000001A");
        var failed = await collector.CollectAsync();
        Assert.Equal(CollectionStatus.Failed, failed.Status);
        Assert.Empty(failed.Items);
        Assert.Equal("CM:0000001A", failed.ErrorCode);
    }

    [Fact]
    public async Task AdapterRenameUsesSameIdentityAndStoresOnlyAllowedFields()
    {
        var id = Guid.NewGuid();
        var source = new FakeNetwork { Batch = SourceBatch<AdapterEntry>.Complete(new AdapterEntry(id, "Old", "Description", "Ethernet", "Down")) };
        var collector = new NetworkCollector(source);
        var before = await collector.CollectAsync();
        source.Batch = SourceBatch<AdapterEntry>.Complete(new AdapterEntry(id, "New", "Description", "Ethernet", "Up"));
        var after = await collector.CollectAsync();
        Assert.Equal(before.Items[0].StableKey, after.Items[0].StableKey);
        Assert.Equal(new[] { "description", "display_name", "interface_type", "operational_status" }, after.Items[0].Fields.Keys);
        var change = Assert.Single(new DiffEngine().Compare(
            new Snapshot(Guid.NewGuid(), Guid.NewGuid(), InventoryCategory.Network, before.CompletedAtUtc, before.Items),
            new Snapshot(Guid.NewGuid(), Guid.NewGuid(), InventoryCategory.Network, after.CompletedAtUtc, after.Items)));
        Assert.Equal(ChangeKind.Modified, change.Kind);
        Assert.Contains(change.Fields, f => f.Name == "display_name");
    }

    [Fact]
    public async Task SourceFailureCannotRemoveExistingDriverOrNetworkBaseline()
    {
        using var store = new TestStore(isDemo: false);
        var devices = new FakeDevices { Batch = SourceBatch<DeviceEntry>.Complete(Device("1")) };
        var network = new FakeNetwork { Batch = SourceBatch<AdapterEntry>.Complete(new AdapterEntry(Guid.NewGuid(), "Adapter", "Demo", "Ethernet", "Up")) };
        var session = new SystemScanSession(store.Repository,
            [new SoftwareCollector(new FakeRegistry()), new StartupCollector(new FakeRegistry(), new FakeFiles()), new DriverCollector(devices), new NetworkCollector(network)]);
        var initial = await session.RunAsync();
        devices.Failure = new InventorySourceException("DeviceListUnstable");
        network.Batch = new SourceBatch<AdapterEntry>([], ["AccessDenied"]);
        var failed = await session.RunAsync();
        Assert.Empty(failed.Changes);
        Assert.Equal(CollectionStatus.Failed, failed.LastAttempt!.Results[2].Status);
        Assert.Equal(CollectionStatus.Partial, failed.LastAttempt.Results[3].Status);
        Assert.Equal(initial.Baselines[2].Id, failed.Baselines[2].Id);
        Assert.Equal(initial.Baselines[3].Id, failed.Baselines[3].Id);
    }

    [Fact]
    public async Task DeviceVersionChangeAndLaterAbsenceUseOneStableIdentity()
    {
        var source = new FakeDevices { Batch = SourceBatch<DeviceEntry>.Complete(Device("1")) };
        var collector = new DriverCollector(source);
        var first = await collector.CollectAsync();
        source.Batch = SourceBatch<DeviceEntry>.Complete(Device("2"));
        var next = await collector.CollectAsync();
        var before = new Snapshot(Guid.NewGuid(), Guid.NewGuid(), InventoryCategory.Drivers, first.CompletedAtUtc, first.Items);
        var after = new Snapshot(Guid.NewGuid(), Guid.NewGuid(), InventoryCategory.Drivers, next.CompletedAtUtc, next.Items);
        var changed = Assert.Single(new DiffEngine().Compare(before, after));
        Assert.Equal(ChangeKind.Modified, changed.Kind);
        Assert.Equal("driver_version", Assert.Single(changed.Fields).Name);
        source.Batch = SourceBatch<DeviceEntry>.Complete();
        var absent = await collector.CollectAsync();
        var removed = Assert.Single(new DiffEngine().Compare(after,
            new Snapshot(Guid.NewGuid(), Guid.NewGuid(), InventoryCategory.Drivers, absent.CompletedAtUtc, absent.Items)));
        Assert.Equal(ChangeKind.Removed, removed.Kind);
        Assert.Contains("PresentDevicesOnly", absent.Warnings);
    }

    [Fact]
    public async Task CollectorCancellationDoesNotPersistAnIncompleteRun()
    {
        using var store = new TestStore(isDemo: false);
        using var cancellation = new CancellationTokenSource();
        var waiting = new WaitingCollector(InventoryCategory.Startup);
        var session = new SystemScanSession(store.Repository,
            [new EmptyCollector(InventoryCategory.Software), waiting, new EmptyCollector(InventoryCategory.Drivers), new EmptyCollector(InventoryCategory.Network)]);
        var pending = session.RunAsync(cancellation.Token);
        await waiting.Started.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(0, (await store.Repository.LoadAsync()).TotalRuns);
    }

    [Fact]
    public async Task UnexpectedCollectorFailureIsIsolatedToItsCategory()
    {
        using var store = new TestStore(isDemo: false);
        var session = new SystemScanSession(store.Repository,
            [new EmptyCollector(InventoryCategory.Software), new EmptyCollector(InventoryCategory.Startup),
             new EmptyCollector(InventoryCategory.Drivers, fail: true), new EmptyCollector(InventoryCategory.Network)]);
        var result = await session.RunAsync();
        Assert.Equal(3, result.Baselines.Count);
        Assert.Equal(CollectionStatus.Failed, result.LastAttempt!.Results[2].Status);
        Assert.Empty(result.Changes);
        Assert.Throws<ArgumentException>(() => new SystemScanSession(store.Repository, []));
    }

    internal static RegistryEntry Software(string name, string version = "1") => new("sample", new Dictionary<string, FieldValue>
    {
        ["DisplayName"] = new(name),
        ["DisplayVersion"] = new(version),
        ["Publisher"] = new("Example")
    });
    internal static DeviceEntry Device(string? version, string? error = null) => new(@"ROOT\EXAMPLE\0001", new Dictionary<string, FieldValue>
    {
        ["display_name"] = new("Sample device"),
        ["driver_version"] = new(version, error)
    });

    internal sealed class FakeRegistry : IRegistrySource
    {
        public Func<RegistryLocation, SourceBatch<RegistryEntry>> Subkeys { get; init; } = _ => SourceBatch<RegistryEntry>.Complete();
        public Func<RegistryLocation, SourceBatch<RegistryValueEntry>> Values { get; init; } = _ => SourceBatch<RegistryValueEntry>.Complete();
        public List<IReadOnlyList<string>> RequestedFields { get; } = [];
        public SourceBatch<RegistryEntry> ReadSubkeys(RegistryLocation location, IReadOnlyList<string> fields, CancellationToken token)
        { token.ThrowIfCancellationRequested(); RequestedFields.Add(fields); return Subkeys(location); }
        public SourceBatch<RegistryValueEntry> ReadValues(RegistryLocation location, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return Values(location); }
    }
    internal sealed class FakeFiles : IStartupFileSource
    {
        public SourceBatch<StartupFileEntry> Batch { get; init; } = SourceBatch<StartupFileEntry>.Complete();
        public SourceBatch<StartupFileEntry> Read(CancellationToken token) => Batch;
    }
    internal sealed class FakeDevices : IDeviceSource
    {
        public SourceBatch<DeviceEntry> Batch { get; set; } = SourceBatch<DeviceEntry>.Complete();
        public Exception? Failure { get; set; }
        public SourceBatch<DeviceEntry> Read(CancellationToken token) => Failure is null ? Batch : throw Failure;
    }
    internal sealed class FakeNetwork : INetworkSource
    {
        public SourceBatch<AdapterEntry> Batch { get; set; } = SourceBatch<AdapterEntry>.Complete();
        public SourceBatch<AdapterEntry> Read(CancellationToken token) => Batch;
    }
    internal sealed class EmptyCollector(InventoryCategory category, bool fail = false) : ISystemCollector
    {
        public InventoryCategory Category => category;
        public Task<CollectionResult> CollectAsync(CancellationToken cancellationToken = default) => fail
            ? throw new InvalidOperationException("Simulated")
            : Task.FromResult(new CollectionResult(category, CollectionStatus.Succeeded, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, []));
    }
    private sealed class WaitingCollector(InventoryCategory category) : ISystemCollector
    {
        public InventoryCategory Category => category;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<CollectionResult> CollectAsync(CancellationToken cancellationToken = default)
        {
            Started.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }
    }
}
