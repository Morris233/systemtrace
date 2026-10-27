using Microsoft.Extensions.Logging;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SystemTrace.Core;

namespace SystemTrace.App.ViewModels;

public partial class MainViewModel
{
    public void ReportStartupIssue() => ShowError("SettingsUnreadable");
    public string VersionText => "v" + typeof(MainViewModel).Assembly.GetName().Version?.ToString(3);
    public bool CanConfigure => application is not null;
    public AppPreferences Preferences => application?.Preferences ?? new AppPreferences();
    public event EventHandler? PreferencesApplied;
    public string ScanProgressText => !IsScanning ? "" : $"{CompletedCollectors} / 4 · " + presenter.Text(ScanningCategory?.ToString() ?? "Saving");
    [ObservableProperty][NotifyPropertyChangedFor(nameof(ScanProgressText))] private bool isScanning;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(ScanProgressText))] private int completedCollectors;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(ScanProgressText))] private InventoryCategory? scanningCategory;
    [ObservableProperty] private bool backgroundEnabled;
    [ObservableProperty] private bool minimizeToTray;
    [ObservableProperty] private bool startAtLogin;
    [ObservableProperty] private string intervalMinutes = "30";
    [ObservableProperty] private string retentionDays = "90";
    [ObservableProperty] private bool redactExports = true;
    [ObservableProperty] private bool isMonitoringPaused;
    [ObservableProperty] private string? statusMessage;
    partial void OnBackgroundEnabledChanged(bool value) { if (!value) MinimizeToTray = false; }
    private bool CanConfigureNow() => CanWork() && CanConfigure;
    private bool CanPauseMonitoring() => CanConfigure && Preferences.BackgroundEnabled;

    private Task SaveAppearanceAsync() => application is null ? Task.CompletedTask :
        WorkAsync(token => application.SaveAppearanceAsync(appearance.Language, appearance.IsDark, token), clearError: false);
    private void LoadPreferences()
    {
        BackgroundEnabled = Preferences.BackgroundEnabled;
        MinimizeToTray = Preferences.MinimizeToTray;
        StartAtLogin = Preferences.StartAtLogin;
        IntervalMinutes = Preferences.IntervalMinutes.ToString(CultureInfo.InvariantCulture);
        RetentionDays = Preferences.RetentionDays.ToString(CultureInfo.InvariantCulture);
    }

    [RelayCommand(CanExecute = nameof(CanConfigureNow))]
    private Task ApplyPreferencesAsync() => WorkAsync(async token =>
    {
        if (!int.TryParse(IntervalMinutes, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
            !int.TryParse(RetentionDays, NumberStyles.None, CultureInfo.InvariantCulture, out var days))
        { ShowError("InvalidPreferences"); return; }
        var desired = new AppPreferences(Language: appearance.Language, IsDark: appearance.IsDark,
            BackgroundEnabled: BackgroundEnabled, MinimizeToTray: MinimizeToTray, StartAtLogin: StartAtLogin,
            IntervalMinutes: minutes, RetentionDays: days);
        try { desired.Validate(); }
        catch (ArgumentException) { ShowError("InvalidPreferences"); return; }
        try { await application!.ApplyAsync(desired, token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Preferences could not be applied");
            ShowError("PreferencesError"); return;
        }
        if (!desired.BackgroundEnabled) IsMonitoringPaused = false;
        OnPropertyChanged(nameof(Preferences));
        PauseMonitoringCommand.NotifyCanExecuteChanged();
        PreferencesApplied?.Invoke(this, EventArgs.Empty);
        StatusMessage = presenter.Text("Saved");
    });

    [RelayCommand(CanExecute = nameof(CanPauseMonitoring))]
    private void PauseMonitoring()
    {
        IsMonitoringPaused = !IsMonitoringPaused;
        PreferencesApplied?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanConfigureNow))]
    private Task PruneHistoryAsync() => WorkAsync(async token =>
    {
        await application!.PruneAsync(token);
        await RefreshAfterMaintenanceAsync(token);
        StatusMessage = presenter.Text("Saved");
    });

    [RelayCommand(CanExecute = nameof(CanConfigureNow))]
    private Task ClearHistoryAsync()
    {
        if (!CanConfigureNow() || !application!.ConfirmClear()) return Task.CompletedTask;
        return WorkAsync(async token =>
        {
            await application.ClearAsync(token);
            BeforeSnapshot = null; AfterSnapshot = null; SnapshotChoices.Clear();
            SelectedTroubleRow = null; TroubleDetail = null; TroubleRows = [];
            troublePage = new([], 0, 0, 40);
            timelineFilter = new(); SearchText = ""; TimelineCategory = FilterOptions[0];
            await RefreshAfterMaintenanceAsync(token);
        });
    }
    private async Task RefreshAfterMaintenanceAsync(CancellationToken token)
    {
        dashboard = await reader.GetDashboardAsync(token);
        await LoadTimelineAsync(token);
        BeforeSnapshot = null; AfterSnapshot = null; SnapshotChoices.Clear(); ResetComparison();
        await LoadSnapshotsAsync(0, token);
        RelabelDashboard(); NotifyPageLabels();
    }
    [RelayCommand(CanExecute = nameof(CanConfigureNow))]
    private Task ExportJsonAsync() => ExportAsync(ReportFormat.Json);
    [RelayCommand(CanExecute = nameof(CanConfigureNow))]
    private Task ExportCsvAsync() => ExportAsync(ReportFormat.Csv);
    private Task ExportAsync(ReportFormat format) => WorkAsync(async token =>
    {
        if (await application!.ExportAsync(format, timelineFilter, RedactExports, token)) StatusMessage = presenter.Text("Exported");
    });
}
