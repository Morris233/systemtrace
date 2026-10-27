using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SystemTrace.App.Services;
using SystemTrace.App.ViewModels;
using SystemTrace.Core;
using SystemTrace.Collectors;
using SystemTrace.Infrastructure;

namespace SystemTrace.App;

public partial class App : Application
{
    private ServiceProvider? services;
    private TrayController? tray;
    private SingleInstance? instance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var folder = Environment.GetEnvironmentVariable("SYSTEMTRACE_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SystemTrace");
        if (!Path.IsPathFullyQualified(folder)) throw new ArgumentException("Data directory must be absolute");
        instance = new SingleInstance(folder);
        if (!instance.IsOwner) { Shutdown(0); return; }
        var preferenceStore = new JsonPreferencesStore(Path.Combine(folder, "settings.json"));
        var settings = new AppPreferences();
        var settingsError = false;
        try { settings = await preferenceStore.LoadAsync(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        { settingsError = true; }
        var login = new WindowsLoginStart(Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable"));
        try { settings = settings with { StartAtLogin = login.IsEnabled }; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { settingsError = true; settings = settings with { StartAtLogin = false }; }
        var resources = new AppearanceService(Resources, settings.Language ?? CultureInfo.CurrentUICulture.Name, settings.IsDark);
        services = new ServiceCollection()
            .AddLogging(builder => builder.AddProvider(new LocalLogProvider(folder)))
            .AddSingleton<IAppearanceService>(resources)
            .AddSingleton<IPreferencesStore>(preferenceStore)
            .AddSingleton<ILoginStart>(login)
            .AddSingleton<SqliteScanRepository>(_ => new SqliteScanRepository(Path.Combine(folder, "system-v1.db"), isDemo: false))
            .AddSingleton<IScanRepository>(provider => provider.GetRequiredService<SqliteScanRepository>())
            .AddSingleton<IHistoryReader>(provider => provider.GetRequiredService<SqliteScanRepository>())
            .AddSingleton<IApplicationServices>(provider =>
            {
                var repository = provider.GetRequiredService<SqliteScanRepository>();
                return new ApplicationServices(preferenceStore, login, repository, repository, resources, settings);
            })
            .AddSingleton<IRegistrySource, WindowsRegistrySource>()
            .AddSingleton<IStartupFileSource, WindowsStartupFileSource>()
            .AddSingleton<IDeviceSource, WindowsDeviceSource>()
            .AddSingleton<INetworkSource, WindowsNetworkSource>()
            .AddSingleton<ISystemCollector, SoftwareCollector>()
            .AddSingleton<ISystemCollector, StartupCollector>()
            .AddSingleton<ISystemCollector, DriverCollector>()
            .AddSingleton<ISystemCollector, NetworkCollector>()
            .AddSingleton<ISystemScanSession>(provider => new SystemScanSession(
                provider.GetRequiredService<IScanRepository>(), provider.GetServices<ISystemCollector>()))
            .AddSingleton<MainViewModel>()
            .AddSingleton<MainWindow>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var model = services.GetRequiredService<MainViewModel>();
        MainWindow = services.GetRequiredService<MainWindow>();
        MainWindow.Show();
        tray = new TrayController((MainWindow)MainWindow, model, resources);
        instance.Listen(() => Dispatcher.BeginInvoke(() => tray?.Show()));
        await model.InitializeAsync();
        if (tray.IsExiting) return;
        if (settingsError) model.ReportStartupIssue();
        if (e.Args.Contains("--login", StringComparer.Ordinal) && settings.BackgroundEnabled && settings.MinimizeToTray) MainWindow.Hide();
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    { tray?.PrepareExit(); base.OnSessionEnding(e); }

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose(); services?.Dispose(); instance?.Dispose();
        base.OnExit(e);
    }
}
