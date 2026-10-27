using SystemTrace.Core;

namespace SystemTrace.Collectors;

public sealed class DriverCollector(IDeviceSource source) : CollectorBase
{
    public override InventoryCategory Category => InventoryCategory.Drivers;
    protected override ReadResult Read(CancellationToken token)
    {
        var batch = source.Read(token);
        var items = batch.Items.Select(device => new SnapshotItem(StableKeys.Device(device.InstanceId), device.Properties)).ToArray();
        return new ReadResult(items, batch.Errors, 0, "PresentDevicesOnly");
    }
}
