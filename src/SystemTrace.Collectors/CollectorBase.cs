using SystemTrace.Core;

namespace SystemTrace.Collectors;

public abstract class CollectorBase : ISystemCollector
{
    public abstract InventoryCategory Category { get; }
    public Task<CollectionResult> CollectAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Collect(cancellationToken), cancellationToken);

    private CollectionResult Collect(CancellationToken token)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            token.ThrowIfCancellationRequested();
            var result = Read(token);
            token.ThrowIfCancellationRequested();
            var errors = result.Errors.Concat(result.Items.SelectMany(item => item.Fields.Values)
                .Where(field => field.ErrorCode is not null).Select(field => field.ErrorCode!)).Distinct(StringComparer.Ordinal).ToArray();
            var status = errors.Length == 0 ? CollectionStatus.Succeeded : CollectionStatus.Partial;
            return new CollectionResult(Category, status, started, DateTimeOffset.UtcNow,
                result.Items, errors.FirstOrDefault(), result.Skipped, result.Warnings.Concat(errors).ToArray());
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception) when (IsReadFailure(exception))
        {
            return new CollectionResult(Category, CollectionStatus.Failed, started, DateTimeOffset.UtcNow, [], ErrorCode(exception));
        }
    }

    protected abstract ReadResult Read(CancellationToken token);
    protected sealed record ReadResult(IReadOnlyList<SnapshotItem> Items, IReadOnlyList<string> Errors, int Skipped = 0, params string[] Warnings);
    internal static bool IsReadFailure(Exception exception) => exception is UnauthorizedAccessException or System.Security.SecurityException
        or IOException or System.ComponentModel.Win32Exception or System.Net.NetworkInformation.NetworkInformationException
        or InvalidOperationException or ArgumentException;
    internal static string ErrorCode(Exception exception) => exception switch
    {
        UnauthorizedAccessException or System.Security.SecurityException => "AccessDenied",
        System.Net.NetworkInformation.NetworkInformationException network => $"Network:{network.ErrorCode}",
        System.ComponentModel.Win32Exception native => $"Win32:{native.NativeErrorCode}",
        InventorySourceException source => source.Code,
        IOException => "ReadFailed",
        _ => "InvalidData"
    };
}
