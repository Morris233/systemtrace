using System.Globalization;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SystemTrace.App.Services;
using SystemTrace.App.ViewModels;
using SystemTrace.Core;
using SystemTrace.Infrastructure;

namespace SystemTrace.App;

public partial class App : Application
{
    private ServiceProvider? services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var resources = new AppearanceService(Resources, CultureInfo.CurrentUICulture.Name);
        services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IAppearanceService>(resources)
            .AddSingleton<IScanRepository>(_ => new SqliteScanRepository(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SystemTrace", "demo-v1.db"), isDemo: true))
            .AddSingleton<IDemoSession, DemoSession>()
            .AddSingleton<MainViewModel>()
            .AddSingleton<MainWindow>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        MainWindow = services.GetRequiredService<MainWindow>();
        MainWindow.Show();
        await services.GetRequiredService<MainViewModel>().InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        services?.Dispose();
        base.OnExit(e);
    }
}
