using SystemTrace.Core;

namespace SystemTrace.Collectors;

public sealed class NetworkCollector(INetworkSource source) : CollectorBase
{
    public override InventoryCategory Category => InventoryCategory.Network;
    protected override ReadResult Read(CancellationToken token)
    {
        var batch = source.Read(token);
        var items = batch.Items.Select(adapter => new SnapshotItem(StableKeys.Adapter(adapter.Id), new Dictionary<string, FieldValue>
        {
            ["display_name"] = new(adapter.Name),
            ["description"] = new(adapter.Description),
            ["interface_type"] = new(adapter.InterfaceType),
            ["operational_status"] = new(adapter.OperationalStatus)
        })).ToArray();
        return new ReadResult(items, batch.Errors, 0, "NetworkOperationalStatusOnly");
    }
}
