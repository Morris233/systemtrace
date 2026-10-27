using System.Globalization;
using Microsoft.Win32;
using SystemTrace.Core;

namespace SystemTrace.Collectors;

public sealed class WindowsRegistrySource : IRegistrySource
{
    public SourceBatch<RegistryEntry> ReadSubkeys(RegistryLocation location, IReadOnlyList<string> fields, CancellationToken token)
    {
        var items = new List<RegistryEntry>();
        var errors = new List<string>();
        try
        {
            using var hive = RegistryKey.OpenBaseKey(location.Hive, location.View);
            using var parent = hive.OpenSubKey(location.Path, writable: false);
            if (parent is null) return SourceBatch<RegistryEntry>.Complete();
            foreach (var name in parent.GetSubKeyNames())
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using var key = parent.OpenSubKey(name, writable: false);
                    if (key is null) { errors.Add("RegistryEntryDisappeared"); continue; }
                    items.Add(new RegistryEntry(name, fields.ToDictionary(field => field, field => ReadField(key, field, textOnly: field != "SystemComponent"))));
                }
                catch (Exception exception) when (CollectorBase.IsReadFailure(exception))
                { errors.Add(CollectorBase.ErrorCode(exception)); }
            }
        }
        catch (Exception exception) when (CollectorBase.IsReadFailure(exception))
        { errors.Add(CollectorBase.ErrorCode(exception)); }
        return new SourceBatch<RegistryEntry>(items, errors);
    }

    public SourceBatch<RegistryValueEntry> ReadValues(RegistryLocation location, CancellationToken token)
    {
        var items = new List<RegistryValueEntry>();
        var errors = new List<string>();
        try
        {
            using var hive = RegistryKey.OpenBaseKey(location.Hive, location.View);
            using var key = hive.OpenSubKey(location.Path, writable: false);
            if (key is null) return SourceBatch<RegistryValueEntry>.Complete();
            foreach (var name in key.GetValueNames())
            {
                token.ThrowIfCancellationRequested();
                var field = ReadField(key, name, textOnly: true);
                if (field.Value is null && field.ErrorCode is null)
                    field = new FieldValue(null, "RegistryEntryDisappeared");
                items.Add(new RegistryValueEntry(name, field));
            }
        }
        catch (Exception exception) when (CollectorBase.IsReadFailure(exception))
        { errors.Add(CollectorBase.ErrorCode(exception)); }
        return new SourceBatch<RegistryValueEntry>(items, errors);
    }

    private static FieldValue ReadField(RegistryKey key, string name, bool textOnly)
    {
        try
        {
            var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return value switch
            {
                null => new(null),
                string text => new(text),
                int number when !textOnly => new(number.ToString(CultureInfo.InvariantCulture)),
                long number when !textOnly => new(number.ToString(CultureInfo.InvariantCulture)),
                _ => new(null, "UnsupportedRegistryType")
            };
        }
        catch (Exception exception) when (CollectorBase.IsReadFailure(exception))
        { return new FieldValue(null, CollectorBase.ErrorCode(exception)); }
    }
}
