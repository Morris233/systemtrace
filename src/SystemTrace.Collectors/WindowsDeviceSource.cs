using System.Runtime.InteropServices;
using System.Text;
using SystemTrace.Core;

namespace SystemTrace.Collectors;

public sealed class InventorySourceException(string code) : IOException(code)
{
    public string Code { get; } = code;
}

public sealed class WindowsDeviceSource : IDeviceSource
{
    private const uint Present = 0x100;
    private const uint BufferSmall = 0x1a;
    private const uint NoSuchValue = 0x25;
    private static readonly Guid DeviceProperties = new("a45c254e-df1c-4efd-8020-67d146a850e0");
    private static readonly Guid DriverProperties = new("a8b865dd-2e3d-4094-ad97-e593a70c75d6");

    public SourceBatch<DeviceEntry> Read(CancellationToken token)
    {
        var ids = ReadIds(token);
        if (ids.Length == 0) throw new InventorySourceException("UnexpectedEmptyDeviceList");
        var items = new List<DeviceEntry>();
        var errors = new List<string>();
        foreach (var id in ids)
        {
            token.ThrowIfCancellationRequested();
            var result = CM_Locate_DevNodeW(out var node, id, 0);
            if (result != 0) { errors.Add($"CM:{result:X8}"); continue; }
            var description = ReadString(node, new PropertyKey(DeviceProperties, 2));
            var friendly = ReadString(node, new PropertyKey(DeviceProperties, 14));
            // A genuinely absent friendly name can fall back to the device description.
            var name = friendly.Value is not null || friendly.ErrorCode is not null ? friendly : description;
            var properties = new Dictionary<string, FieldValue>
            {
                ["display_name"] = name,
                ["class"] = ReadString(node, new PropertyKey(DeviceProperties, 9)),
                ["manufacturer"] = ReadString(node, new PropertyKey(DeviceProperties, 13)),
                ["driver_provider"] = ReadString(node, new PropertyKey(DriverProperties, 9)),
                ["driver_version"] = ReadString(node, new PropertyKey(DriverProperties, 3)),
                ["driver_date"] = ReadDate(node, new PropertyKey(DriverProperties, 2))
            };
            items.Add(new DeviceEntry(id, properties));
        }
        return new SourceBatch<DeviceEntry>(items, errors);
    }

    private static string[] ReadIds(CancellationToken token)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var result = CM_Get_Device_ID_List_SizeW(out var length, null, Present);
            if (result != 0) throw new InventorySourceException($"CM:{result:X8}");
            if (length > 1_000_000) throw new InventorySourceException("DeviceListTooLarge");
            var buffer = new char[Math.Max(length, 2)];
            result = CM_Get_Device_ID_ListW(null, buffer, (uint)buffer.Length, Present);
            if (result == BufferSmall) continue;
            if (result != 0) throw new InventorySourceException($"CM:{result:X8}");
            return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }
        throw new InventorySourceException("DeviceListUnstable");
    }

    private static (byte[]? Bytes, string? Error) ReadProperty(uint node, PropertyKey key, uint expectedType)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            uint size = 0;
            var result = CM_Get_DevNode_PropertyW(node, ref key, out _, null, ref size, 0);
            if (result == NoSuchValue) return (null, null);
            if (result != 0 && result != BufferSmall) return (null, $"CM:{result:X8}");
            if (size > 65536) return (null, "DevicePropertyTooLarge");
            var buffer = new byte[Math.Max(size, 2)];
            size = (uint)buffer.Length;
            result = CM_Get_DevNode_PropertyW(node, ref key, out var type, buffer, ref size, 0);
            if (result == BufferSmall) continue;
            if (result == NoSuchValue) return (null, null);
            if (result != 0) return (null, $"CM:{result:X8}");
            if (type != expectedType || size > buffer.Length) return (null, "UnexpectedDevicePropertyType");
            return (buffer[..(int)size], null);
        }
        return (null, "DevicePropertyUnstable");
    }

    private static FieldValue ReadString(uint node, PropertyKey key)
    {
        var (bytes, error) = ReadProperty(node, key, 0x12);
        if (error is not null || bytes is null) return new FieldValue(null, error);
        if (bytes.Length % 2 != 0) return new FieldValue(null, "InvalidUnicodeProperty");
        return new FieldValue(Encoding.Unicode.GetString(bytes).TrimEnd('\0'));
    }

    private static FieldValue ReadDate(uint node, PropertyKey key)
    {
        var (bytes, error) = ReadProperty(node, key, 0x10);
        if (error is not null || bytes is null) return new FieldValue(null, error);
        if (bytes.Length != 8) return new FieldValue(null, "InvalidFileTime");
        try { return new FieldValue(DateTimeOffset.FromFileTime(BitConverter.ToInt64(bytes)).ToUniversalTime().ToString("O")); }
        catch (ArgumentOutOfRangeException) { return new FieldValue(null, "InvalidFileTime"); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey(Guid format, uint property)
    {
        public Guid Format = format;
        public uint Property = property;
    }

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Get_Device_ID_List_SizeW(out uint length, string? filter, uint flags);
    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Get_Device_ID_ListW(string? filter, [Out] char[] buffer, uint length, uint flags);
    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Locate_DevNodeW(out uint node, string id, uint flags);
    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Get_DevNode_PropertyW(uint node, ref PropertyKey key, out uint type, [Out] byte[]? buffer, ref uint size, uint flags);
}
