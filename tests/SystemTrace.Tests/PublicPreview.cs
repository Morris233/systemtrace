using SystemTrace.Core;

namespace SystemTrace.Tests;

// Only used for public documentation screenshots; never linked into the production application.
internal static class PublicPreview
{
    public static ScanRun Run(bool updated)
    {
        var time = DateTimeOffset.Parse(updated ? "2026-10-27T02:30:00Z" : "2026-10-27T02:00:00Z");
        SnapshotItem Item(string key, params (string Name, string Value)[] fields) =>
            new("preview/" + key, fields.ToDictionary(field => field.Name, field => new FieldValue(field.Value)));
        var entries = new Dictionary<InventoryCategory, SnapshotItem[]>
        {
            [InventoryCategory.Software] = [
                Item("software/editor", ("display_name","Meridian Editor"), ("version",updated ? "1.5.0" : "1.4.0"), ("publisher","Example Studio")),
                Item("software/tools", ("display_name","Photo Tools"), ("version","2.6.0"), ("publisher","Example Studio"))],
            [InventoryCategory.Startup] = [Item("startup/updater", ("display_name","Meridian updater"),
                ("command",updated ? @"C:\Program Files\Meridian\update.exe --silent" : @"C:\Program Files\Meridian\update.exe"), ("source","Run"))],
            [InventoryCategory.Drivers] = [Item("driver/display", ("display_name","Display adapter"),
                ("driver_version",updated ? "31.1.0" : "31.0.1"), ("class","Display"))],
            [InventoryCategory.Network] = [Item("network/ethernet", ("display_name","Ethernet"), ("interface_type","Ethernet"),
                ("operational_status",updated ? "Up" : "Down"))]
        };
        return new(Guid.NewGuid(), time, time, false, entries.Select(pair =>
            new CollectionResult(pair.Key, CollectionStatus.Succeeded, time, time, pair.Value)).ToArray());
    }
}
