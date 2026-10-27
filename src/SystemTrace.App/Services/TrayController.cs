using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using SystemTrace.App.ViewModels;
using SystemTrace.Core;
using Forms = System.Windows.Forms;

namespace SystemTrace.App.Services;

public sealed class TrayController : IDisposable
{
    private readonly MainWindow window;
    private readonly MainViewModel model;
    private readonly IAppearanceService appearance;
    private readonly Forms.NotifyIcon icon;
    private readonly System.Drawing.Icon trayIcon;
    private readonly Forms.ContextMenuStrip menu = new();
    private readonly Forms.ToolStripMenuItem open = new(), scan = new(), pause = new(), exit = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly MonitorSchedule schedule = new();
    private bool exiting;
    private bool disposed;

    public TrayController(MainWindow window, MainViewModel model, IAppearanceService appearance)
    {
        this.window = window; this.model = model; this.appearance = appearance;
        menu.Items.AddRange([open, scan, pause, new Forms.ToolStripSeparator(), exit]);
        using var iconStream = typeof(TrayController).Assembly.GetManifestResourceStream("SystemTrace.Icon")!;
        trayIcon = new System.Drawing.Icon(iconStream);
        icon = new Forms.NotifyIcon { Text = "SystemTrace", Icon = trayIcon, ContextMenuStrip = menu };
        open.Click += (_, _) => Show();
        icon.DoubleClick += (_, _) => Show();
        scan.Click += async (_, _) => { if (model.ScanCommand.CanExecute(null)) await model.ScanCommand.ExecuteAsync(null); };
        pause.Click += (_, _) => { if (model.PauseMonitoringCommand.CanExecute(null)) model.PauseMonitoringCommand.Execute(null); };
        exit.Click += (_, _) => Exit();
        window.Closing += OnClosing;
        model.PropertyChanged += OnModelChanged;
        model.PreferencesApplied += OnPreferences;
        timer.Tick += OnTick;
        Refresh();
    }
    public bool IsExiting => exiting;
    public void Show()
    {
        if (disposed || exiting) return;
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }
    public void Exit()
    {
        PrepareExit();
        window.Close();
    }
    public void PrepareExit() { exiting = true; timer.Stop(); model.CancelPending(); }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!exiting && model.Preferences.BackgroundEnabled && model.Preferences.MinimizeToTray)
        { e.Cancel = true; window.Hide(); }
        else PrepareExit();
    }
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsBusy) or nameof(MainViewModel.DisplayLanguage) or nameof(MainViewModel.IsMonitoringPaused))
            Refresh();
    }
    private void OnPreferences(object? sender, EventArgs e) => Refresh();
    private void Refresh()
    {
        if (disposed || exiting) return;
        var settings = model.Preferences;
        schedule.Configure(settings.BackgroundEnabled, model.IsMonitoringPaused, settings.IntervalMinutes, DateTimeOffset.UtcNow);
        if (settings.BackgroundEnabled && !model.IsMonitoringPaused) timer.Start(); else timer.Stop();
        if (!settings.BackgroundEnabled && !window.IsVisible) Show();
        icon.Visible = settings.BackgroundEnabled;
        open.Text = appearance.Text("Open");
        scan.Text = appearance.Text("ScanNow");
        scan.Enabled = model.ScanCommand.CanExecute(null);
        pause.Text = appearance.Text(model.IsMonitoringPaused ? "ResumeMonitoring" : "PauseMonitoring");
        pause.Enabled = settings.BackgroundEnabled;
        exit.Text = appearance.Text("Exit");
    }
    private async void OnTick(object? sender, EventArgs e)
    {
        if (!disposed && schedule.TakeDue(DateTimeOffset.UtcNow, model.ScanCommand.CanExecute(null)))
            await model.ScanCommand.ExecuteAsync(null);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; timer.Stop(); timer.Tick -= OnTick;
        window.Closing -= OnClosing; model.PropertyChanged -= OnModelChanged; model.PreferencesApplied -= OnPreferences;
        icon.Visible = false; icon.Dispose(); trayIcon.Dispose(); menu.Dispose();
    }
}
