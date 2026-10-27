using System.IO;
using System.Windows;
using Microsoft.Win32;
using SystemTrace.Core;

namespace SystemTrace.App.Services;

public interface IApplicationServices
{
    AppPreferences Preferences { get; }
    Task ApplyAsync(AppPreferences preferences, CancellationToken token);
    Task SaveAppearanceAsync(string language, bool dark, CancellationToken token);
    Task PruneAsync(CancellationToken token);
    Task ClearAsync(CancellationToken token);
    bool ConfirmClear();
    Task<bool> ExportAsync(ReportFormat format, ChangeQuery query, bool redact, CancellationToken token);
}
public sealed class ApplicationServices(IPreferencesStore store, ILoginStart login, IHistoryMaintenance maintenance,
    IReportExporter exporter, IAppearanceService appearance, AppPreferences preferences) : IApplicationServices
{
    public AppPreferences Preferences { get; private set; } = preferences;

    public async Task SaveAppearanceAsync(string language, bool dark, CancellationToken token)
    {
        var desired = Preferences with { Language = language, IsDark = dark };
        await store.SaveAsync(desired, token);
        Preferences = desired;
    }
    public async Task ApplyAsync(AppPreferences desired, CancellationToken token)
    {
        desired.Validate();
        var previous = Preferences;
        await store.SaveAsync(desired, token);
        try
        {
            if (desired.StartAtLogin != previous.StartAtLogin || desired.StartAtLogin && !login.IsEnabled)
                login.SetEnabled(desired.StartAtLogin);
        }
        catch
        {
            await store.SaveAsync(previous, CancellationToken.None);
            throw;
        }
        Preferences = desired;
    }
    public Task PruneAsync(CancellationToken token) => Preferences.RetentionDays == 0 ? Task.CompletedTask :
        maintenance.PruneAsync(DateTimeOffset.UtcNow.AddDays(-Preferences.RetentionDays), token);
    public Task ClearAsync(CancellationToken token) => maintenance.ClearAsync(token);
    public bool ConfirmClear() => MessageBox.Show(appearance.Text("ClearConfirmation"), "SystemTrace",
        MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    public async Task<bool> ExportAsync(ReportFormat format, ChangeQuery query, bool redact, CancellationToken token)
    {
        if (!redact && MessageBox.Show(appearance.Text("RawExportConfirmation"), "SystemTrace",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return false;
        var extension = format == ReportFormat.Json ? "json" : "csv";
        var dialog = new SaveFileDialog
        {
            FileName = "SystemTrace-report." + extension,
            DefaultExt = "." + extension,
            Filter = extension.ToUpperInvariant() + "|*." + extension,
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog() != true) return false;
        var temporary = dialog.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                await exporter.ExportAsync(stream, format, query, redact, token);
                await stream.FlushAsync(token);
                stream.Flush(true);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, dialog.FileName, true);
            return true;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
