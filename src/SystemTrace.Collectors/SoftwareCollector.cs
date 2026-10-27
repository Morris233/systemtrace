using Microsoft.Win32;
using SystemTrace.Core;

namespace SystemTrace.Collectors;

public sealed class SoftwareCollector(IRegistrySource source, IReadOnlyList<RegistryLocation>? locations = null) : CollectorBase
{
    public const string UninstallPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
    public override InventoryCategory Category => InventoryCategory.Software;
    private static readonly string[] Fields = ["DisplayName", "DisplayVersion", "Publisher", "SystemComponent"];
    public static IReadOnlyList<RegistryLocation> DefaultLocations { get; } = Array.AsReadOnly(new[]
    {
        new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Registry64, UninstallPath),
        new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry64, UninstallPath),
        new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry32, UninstallPath)
    });

    protected override ReadResult Read(CancellationToken token)
    {
        var items = new List<SnapshotItem>();
        var errors = new List<string>();
        var skipped = 0;
        foreach (var location in locations ?? DefaultLocations)
        {
            token.ThrowIfCancellationRequested();
            var batch = source.ReadSubkeys(location, Fields, token);
            errors.AddRange(batch.Errors);
            foreach (var entry in batch.Items)
            {
                token.ThrowIfCancellationRequested();
                var name = entry.Values.GetValueOrDefault("DisplayName") ?? new FieldValue(null);
                if (name.ErrorCode is not null) errors.Add(name.ErrorCode);
                if (string.IsNullOrWhiteSpace(name.Value)) { skipped++; continue; }
                items.Add(new SnapshotItem(StableKeys.Software(location.Hive.ToString(), location.View.ToString(), location.Path + "\\" + entry.Subkey),
                    new Dictionary<string, FieldValue>
                    {
                        ["display_name"] = name,
                        ["version"] = entry.Values.GetValueOrDefault("DisplayVersion") ?? new(null),
                        ["publisher"] = entry.Values.GetValueOrDefault("Publisher") ?? new(null),
                        ["system_component"] = entry.Values.GetValueOrDefault("SystemComponent") ?? new(null),
                        ["registry_hive"] = new(location.Hive.ToString()),
                        ["registry_view"] = new(location.View.ToString()),
                        ["registry_subkey"] = new(location.Path + "\\" + entry.Subkey)
                    }));
            }
        }
        return new ReadResult(items, errors, skipped, "SoftwareCoverageLimited");
    }
}
