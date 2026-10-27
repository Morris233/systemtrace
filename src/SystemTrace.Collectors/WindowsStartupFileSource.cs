namespace SystemTrace.Collectors;

public sealed class WindowsStartupFileSource(IReadOnlyList<(string Scope, string Path)>? folders = null) : IStartupFileSource
{
    public SourceBatch<StartupFileEntry> Read(CancellationToken token)
    {
        var locations = folders ?? new[]
        {
            ("CurrentUser", Environment.GetFolderPath(Environment.SpecialFolder.Startup)),
            ("AllUsers", Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup))
        };
        var entries = new List<StartupFileEntry>();
        var errors = new List<string>();
        foreach (var (scope, path) in locations)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(path)) { errors.Add("StartupFolderUnavailable"); continue; }
            try
            {
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var info = new FileInfo(file);
                        info.Refresh();
                        if (!info.Exists) { errors.Add("StartupFileDisappeared"); continue; }
                        entries.Add(new StartupFileEntry(scope, info.Name, new DateTimeOffset(info.LastWriteTimeUtc)));
                    }
                    catch (Exception exception) when (CollectorBase.IsReadFailure(exception))
                    { errors.Add(CollectorBase.ErrorCode(exception)); }
                }
            }
            catch (DirectoryNotFoundException) { /* An absent startup directory is a known empty source. */ }
            catch (Exception exception) when (CollectorBase.IsReadFailure(exception))
            { errors.Add(CollectorBase.ErrorCode(exception)); }
        }
        return new SourceBatch<StartupFileEntry>(entries, errors);
    }
}
