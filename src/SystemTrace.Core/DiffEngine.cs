using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SystemTrace.Core;

public sealed class DiffEngine
{
    public IReadOnlyList<Change> Compare(Snapshot? previous, Snapshot current, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (previous is null) return Array.Empty<Change>();
        if (previous.Category != current.Category || current.ObservedAtUtc < previous.ObservedAtUtc)
            throw new ArgumentException("Incompatible category or observation interval", nameof(current));
        var before = previous.Items.ToDictionary(i => i.StableKey, StringComparer.Ordinal);
        var after = current.Items.ToDictionary(i => i.StableKey, StringComparer.Ordinal);
        var changes = new List<Change>();
        foreach (var key in before.Keys.Union(after.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            before.TryGetValue(key, out var oldItem);
            after.TryGetValue(key, out var newItem);
            var names = (oldItem?.Fields.Keys ?? []).Union(newItem?.Fields.Keys ?? [], StringComparer.Ordinal).Order(StringComparer.Ordinal);
            var deltas = names.Select(name => new FieldDelta(name,
                    oldItem?.Fields.GetValueOrDefault(name), newItem?.Fields.GetValueOrDefault(name)))
                .Where(field => field.Before != field.After).ToArray();
            if (oldItem is not null && newItem is not null && deltas.Length == 0) continue;
            var kind = oldItem is null ? ChangeKind.Added : newItem is null ? ChangeKind.Removed : ChangeKind.Modified;
            var identity = JsonSerializer.Serialize(new { previous.Id, Current = current.Id, current.Category, Key = key, Kind = kind });
            var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
            changes.Add(new Change(id, current.Category, key, kind, previous.ObservedAtUtc, current.ObservedAtUtc,
                oldItem, newItem, Array.AsReadOnly(deltas)));
        }
        return changes.AsReadOnly();
    }
}
