namespace SystemTrace.Core;

public interface ISystemCollector
{
    InventoryCategory Category { get; }
    Task<CollectionResult> CollectAsync(CancellationToken cancellationToken = default);
}

public interface ISystemScanSession
{
    Task<InventoryHistory> LoadAsync(CancellationToken cancellationToken = default);
    Task<InventoryHistory> RunAsync(CancellationToken cancellationToken = default);
}
