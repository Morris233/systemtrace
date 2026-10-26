using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using SystemTrace.App;
using SystemTrace.App.Services;
using SystemTrace.App.ViewModels;
using SystemTrace.Core;
using SystemTrace.Infrastructure;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace SystemTrace.Tests;

public sealed class ShellTests
{
    [Theory]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh-TW", "zh-CN")]
    [InlineData("ZH-hans", "zh-CN")]
    [InlineData("en-US", "en-US")]
    [InlineData("fr-FR", "en-US")]
    [InlineData("", "en-US")]
    [InlineData(null, "en-US")]
    public void LanguageSelectionHasSupportedFallback(string? culture, string expected) =>
        Assert.Equal(expected, LanguagePolicy.Resolve(culture));

    [Fact]
    public void DemoIsExplicitAndContainsOnlyFourSyntheticCategories()
    {
        var first = DemoSession.CreateRun(0, DateTimeOffset.UtcNow);
        Assert.True(first.IsDemo);
        Assert.Equal(Enum.GetValues<InventoryCategory>(), first.Results.Select(x => x.Category));
        Assert.All(first.Results.SelectMany(result => result.Items), item => Assert.StartsWith("demo/", item.StableKey));
    }

    [Fact]
    public void CoreDoesNotReferencePlatformOrPersistenceAssemblies()
    {
        var references = typeof(IScanRepository).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, a => a.Name?.Contains("Windows", StringComparison.OrdinalIgnoreCase) == true
            || a.Name?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true
            || a.Name?.StartsWith("Presentation", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void NavigationAndGeneratedCommandsWork()
    {
        var appearance = new FakeAppearance();
        var vm = CreateViewModel(appearance);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.Equal(5, vm.Pages.Count);
        for (var i = 0; i < vm.Pages.Count; i++)
        {
            vm.SelectedPageIndex = i;
            Assert.Same(vm.Pages[i], vm.CurrentPage);
        }
        Assert.Contains(nameof(MainViewModel.CurrentPage), changed);
        vm.SelectedPageIndex = -1;
        Assert.Same(vm.Pages[0], vm.CurrentPage);
        vm.SelectedPageIndex = 99;
        Assert.Same(vm.Pages[0], vm.CurrentPage);
        vm.ToggleLanguageCommand.Execute(null);
        vm.ToggleThemeCommand.Execute(null);
        Assert.Equal("zh-CN", appearance.Language);
        Assert.False(appearance.IsDark);
    }

    [Fact]
    public void RealWpfShellRendersEveryPageAndUpdatesResourcesLive()
    {
        RunSta(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var bindingErrors = new StringWriter();
            var listener = new TextWriterTraceListener(bindingErrors);
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            try
            {
                var appearance = new AppearanceService(app.Resources, "en-US");
                app.Resources.MergedDictionaries.Add(Load("Styles.xaml"));
                var english = Load("Strings/en-US.xaml");
                var chinese = Load("Strings/zh-CN.xaml");
                var keys = english.Keys.Cast<string>().Order().ToArray();
                Assert.Equal(keys, chinese.Keys.Cast<string>().Order().ToArray());
                Assert.All(keys, key =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(english[key] as string));
                    Assert.False(string.IsNullOrWhiteSpace(chinese[key] as string));
                });
                Assert.Contains("DEMO", app.FindResource("DemoNotice").ToString());
                using var store = new TestStore();
                var vm = new MainViewModel(new DemoSession(store.Repository), appearance, NullLogger<MainViewModel>.Instance);
                Pump(vm.InitializeAsync());
                Assert.Equal(0, vm.History.TotalRuns);
                Pump(vm.RunDemoCommand.ExecuteAsync(null));
                Assert.Empty(vm.History.Changes);
                Pump(vm.RunDemoCommand.ExecuteAsync(null));
                Assert.Equal(4, vm.History.Changes.Count);
                var window = new MainWindow(vm) { Width = 960, Height = 620, ShowInTaskbar = false };
                window.Show();
                try
                {
                    foreach (var language in new[] { "en-US", "zh-CN" })
                    {
                        if (appearance.Language != language) vm.ToggleLanguageCommand.Execute(null);
                        foreach (var dark in new[] { true, false })
                        {
                            if (appearance.IsDark != dark) vm.ToggleThemeCommand.Execute(null);
                            Assert.Equal(language == "en-US" ? "Overview" : "概览", app.FindResource("Overview"));
                            var color = ((SolidColorBrush)app.FindResource("BackgroundBrush")).Color;
                            Assert.Equal(dark ? Color.FromRgb(17, 19, 28) : Color.FromRgb(246, 245, 242), color);
                            for (var page = 0; page < 5; page++)
                            {
                                vm.SelectedPageIndex = page;
                                window.UpdateLayout();
                                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                                window.UpdateLayout();
                                var view = Descendants(window).OfType<UserControl>().Single();
                                Assert.True(view.ActualWidth > 0);
                                var renderedText = Descendants(window).OfType<TextBlock>().Select(t => t.Text).ToArray();
                                Assert.Contains(renderedText, text => text.Contains("DEMO", StringComparison.Ordinal));
                                var titleKey = new[] { "OverviewTitle", "TimelineTitle", "SnapshotsTitle", "TroubleshootTitle", "SettingsTitle" }[page];
                                Assert.Contains(app.FindResource(titleKey).ToString(), renderedText);
                                SavePreview(window, $"{language}-{(dark ? "dark" : "light")}-{page}");
                            }
                        }
                    }
                    // Repeated switching must not accumulate dictionaries.
                    Assert.Equal(3, app.Resources.MergedDictionaries.Count);
                    window.Width = 1180;
                    window.Height = 780;
                    vm.SelectedPageIndex = 0;
                    if (!appearance.IsDark) vm.ToggleThemeCommand.Execute(null);
                    window.UpdateLayout();
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    SavePreview(window, "overview-zh-CN-dark");
                }
                finally { window.Close(); }
                listener.Flush();
                Assert.True(string.IsNullOrWhiteSpace(bindingErrors.ToString()), bindingErrors.ToString());
            }
            finally
            {
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
                listener.Dispose();
                app.Shutdown();
            }
        });
    }

    [Fact]
    public void ApplicationStartsAndExitsWithoutAResidentProcess()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "SystemTrace.slnx")))
            root = root.Parent;
        Assert.NotNull(root);
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var executable = Path.Combine(root.FullName, "src", "SystemTrace.App", "bin", configuration,
            "net10.0-windows", "SystemTrace.App.exe");
        using var process = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false });
        Assert.NotNull(process);
        try
        {
            Assert.True(process.WaitForInputIdle(15000), "Application did not become responsive");
            Assert.True(SpinWait.SpinUntil(() =>
            {
                process.Refresh();
                return process.HasExited || process.MainWindowHandle != IntPtr.Zero;
            }, TimeSpan.FromSeconds(15)), "Main window did not appear");
            process.Refresh();
            Assert.False(process.HasExited, "Application exited during startup");
            Assert.Equal("SystemTrace", process.MainWindowTitle);
            Assert.True(process.CloseMainWindow(), "Main window did not accept close");
            Assert.True(process.WaitForExit(10000), "Application remained resident after closing");
            Assert.Equal(0, process.ExitCode);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    private static ResourceDictionary Load(string path) => new()
    {
        Source = new Uri($"/SystemTrace.App;component/Resources/{path}", UriKind.Relative)
    };

    private static MainViewModel CreateViewModel(IAppearanceService appearance) =>
        new(new EmptySession(), appearance, NullLogger<MainViewModel>.Instance);

    private static void Pump(Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var dispatcher = Dispatcher.CurrentDispatcher;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }

    private sealed class EmptySession : IDemoSession
    {
        public Task<InventoryHistory> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(InventoryHistory.Empty);
        public Task<InventoryHistory> RunAsync(CancellationToken cancellationToken = default) => Task.FromResult(InventoryHistory.Empty);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void SavePreview(Window window, string name)
    {
        var folder = Environment.GetEnvironmentVariable("SYSTEMTRACE_QA_DIR");
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        var content = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name + ".png"));
        encoder.Save(stream);
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "WPF test timed out");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class FakeAppearance : IAppearanceService
    {
        public string Language { get; private set; } = "en-US";
        public bool IsDark { get; private set; } = true;
        public string Text(string key) => key;
        public void ToggleLanguage() => Language = Language == "en-US" ? "zh-CN" : "en-US";
        public void ToggleTheme() => IsDark = !IsDark;
    }
}
