using Microsoft.Win32;
using SystemTrace.Core;

namespace SystemTrace.Collectors;

public sealed class StartupCollector(IRegistrySource registry, IStartupFileSource files,
    IReadOnlyList<RegistryLocation>? locations = null) : CollectorBase
{
    public override InventoryCategory Category => InventoryCategory.Startup;
    public static IReadOnlyList<RegistryLocation> DefaultLocations { get; } = Array.AsReadOnly(new[] { "Run", "RunOnce" }
        .SelectMany(name => SoftwareCollector.DefaultLocations.Select(location => location with
        { Path = @"Software\Microsoft\Windows\CurrentVersion\" + name })).ToArray());

    protected override ReadResult Read(CancellationToken token)
    {
        var items = new List<SnapshotItem>();
        var errors = new List<string>();
        var skipped = 0;
        foreach (var location in locations ?? DefaultLocations)
        {
            token.ThrowIfCancellationRequested();
            var batch = registry.ReadValues(location, token);
            errors.AddRange(batch.Errors);
            foreach (var entry in batch.Items)
            {
                if (entry.Command.ErrorCode is not null) errors.Add(entry.Command.ErrorCode);
                if (entry.Name.Length == 0) { skipped++; continue; }
                items.Add(new SnapshotItem(StableKeys.StartupRegistry(location.Hive.ToString(), location.View.ToString(), location.Path, entry.Name),
                    new Dictionary<string, FieldValue>
                    {
                        ["display_name"] = new(entry.Name),
                        ["command"] = entry.Command,
                        ["source"] = new(location.Path.EndsWith("RunOnce", StringComparison.OrdinalIgnoreCase) ? "RunOnce" : "Run"),
                        ["registry_hive"] = new(location.Hive.ToString()),
                        ["registry_view"] = new(location.View.ToString()),
                        ["registry_key"] = new(location.Path)
                    }));
            }
        }
        var fileBatch = files.Read(token);
        errors.AddRange(fileBatch.Errors);
        foreach (var file in fileBatch.Items)
            items.Add(new SnapshotItem(StableKeys.StartupFile(file.Scope, file.RelativePath), new Dictionary<string, FieldValue>
            {
                ["display_name"] = new(file.RelativePath),
                ["source"] = new("StartupFolder"),
                ["scope"] = new(file.Scope),
                ["relative_path"] = new(file.RelativePath),
                ["last_write_utc"] = new(file.LastWriteUtc.ToUniversalTime().ToString("O"))
            }));
        return new ReadResult(items, errors, skipped, "StartupCoverageLimited", "RunOnceTransient");
    }
}
