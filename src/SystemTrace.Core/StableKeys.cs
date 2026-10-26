using System.Globalization;
using System.Text;

namespace SystemTrace.Core;

public static class StableKeys
{
    private static string Identity(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Normalize(NormalizationForm.FormC).Replace('/', '\\').ToUpperInvariant();
    }

    private static string Join(string kind, params string[] parts) => kind + ":" +
        string.Concat(parts.Select(Identity).Select(p => p.Length.ToString(CultureInfo.InvariantCulture) + ":" + p));

    public static string Software(string hive, string view, string subkey) => Join("software", hive, view, subkey);
    public static string StartupRegistry(string hive, string view, string key, string valueName) =>
        Join("startup-registry", hive, view, key, valueName);
    public static string StartupFile(string scope, string relativePath) => Join("startup-file", scope, relativePath);
    public static string Device(string instanceId) => Join("device", instanceId);
    public static string Adapter(Guid interfaceId) => interfaceId == Guid.Empty
        ? throw new ArgumentException("Adapter ID must not be empty", nameof(interfaceId))
        : Join("adapter", interfaceId.ToString("D"));
}
