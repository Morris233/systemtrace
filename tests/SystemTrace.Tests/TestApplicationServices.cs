using SystemTrace.App.Services;
using SystemTrace.Core;

namespace SystemTrace.Tests;

internal sealed class TestApplicationServices(AppPreferences? initial = null, IHistoryMaintenance? maintenance = null) : IApplicationServices
{
    public AppPreferences Preferences { get; private set; } = initial ?? new AppPreferences();
    public bool AllowClear { get; set; }
    public int ClearCalls { get; private set; }
    public ChangeQuery? ExportedQuery { get; private set; }
    public bool ExportRedacted { get; private set; }
    public Task ApplyAsync(AppPreferences preferences, CancellationToken token)
    { preferences.Validate(); Preferences = preferences; return Task.CompletedTask; }
    public Task SaveAppearanceAsync(string language, bool dark, CancellationToken token)
    { Preferences = Preferences with { Language = language, IsDark = dark }; return Task.CompletedTask; }
    public Task PruneAsync(CancellationToken token) => Task.CompletedTask;
    public Task ClearAsync(CancellationToken token) { ClearCalls++; return maintenance?.ClearAsync(token) ?? Task.CompletedTask; }
    public bool ConfirmClear() => AllowClear;
    public Task<bool> ExportAsync(ReportFormat format, ChangeQuery query, bool redact, CancellationToken token)
    { ExportedQuery = query; ExportRedacted = redact; return Task.FromResult(true); }
}
