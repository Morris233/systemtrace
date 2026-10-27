using Microsoft.Win32;
using SystemTrace.Core;

namespace SystemTrace.Collectors;

public sealed record SourceBatch<T>(IReadOnlyList<T> Items, IReadOnlyList<string> Errors)
{
    public static SourceBatch<T> Complete(params T[] items) => new(items, []);
}
public sealed record RegistryLocation(RegistryHive Hive, RegistryView View, string Path);
public sealed record RegistryEntry(string Subkey, IReadOnlyDictionary<string, FieldValue> Values);
public sealed record RegistryValueEntry(string Name, FieldValue Command);
public interface IRegistrySource
{
    SourceBatch<RegistryEntry> ReadSubkeys(RegistryLocation location, IReadOnlyList<string> fields, CancellationToken token);
    SourceBatch<RegistryValueEntry> ReadValues(RegistryLocation location, CancellationToken token);
}
public sealed record StartupFileEntry(string Scope, string RelativePath, DateTimeOffset LastWriteUtc);
public interface IStartupFileSource
{
    SourceBatch<StartupFileEntry> Read(CancellationToken token);
}
public sealed record DeviceEntry(string InstanceId, IReadOnlyDictionary<string, FieldValue> Properties);
public interface IDeviceSource
{
    SourceBatch<DeviceEntry> Read(CancellationToken token);
}
public sealed record AdapterEntry(Guid Id, string Name, string Description, string InterfaceType, string OperationalStatus);
public interface INetworkSource
{
    SourceBatch<AdapterEntry> Read(CancellationToken token);
}
