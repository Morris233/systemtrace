using System.Text.Json;
using SystemTrace.Core;

namespace SystemTrace.Infrastructure;

public sealed class JsonPreferencesStore(string path) : IPreferencesStore
{
    public async Task<AppPreferences> LoadAsync(CancellationToken token = default)
    {
        if (!File.Exists(path)) return new AppPreferences();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        var preferences = await JsonSerializer.DeserializeAsync<AppPreferences>(stream, cancellationToken: token)
            ?? throw new InvalidDataException("Empty preferences");
        preferences.Validate();
        return preferences;
    }

    public async Task SaveAsync(AppPreferences preferences, CancellationToken token = default)
    {
        preferences.Validate();
        var target = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                await JsonSerializer.SerializeAsync(stream, preferences, cancellationToken: token);
                await stream.FlushAsync(token);
                stream.Flush(flushToDisk: true);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, target, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
