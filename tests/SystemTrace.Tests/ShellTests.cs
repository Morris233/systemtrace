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
using SystemTrace.Collectors;
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
    public void ProductionPagesRenderAndRetainFiltersDetailsAcrossLanguages()
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
                var english = Load("Strings/en-US.xaml"); var chinese = Load("Strings/zh-CN.xaml");
                var keys = english.Keys.Cast<string>().Order().ToArray();
                Assert.Equal(keys, chinese.Keys.Cast<string>().Order().ToArray());
                Assert.All(keys, key => { Assert.False(string.IsNullOrWhiteSpace(english[key] as string)); Assert.False(string.IsNullOrWhiteSpace(chinese[key] as string)); });
                using var store = new TestStore(false);
                Pump(store.Repository.CommitAsync(HistoryReaderTests.Run(0, 55)));
                Pump(store.Repository.CommitAsync(HistoryReaderTests.Run(1, 55)));
                var vm = new MainViewModel(new EmptySession(), store.Repository, appearance, NullLogger<MainViewModel>.Instance);
                Pump(vm.InitializeAsync());
                Assert.Equal(2, vm.Dashboard.TotalRuns);
                Assert.Equal(40, vm.TimelineRows.Count);
                Assert.True(vm.NextTimelineCommand.CanExecute(null));
                Pump(vm.NextTimelineCommand.ExecuteAsync(null));
                Assert.Null(vm.TimelineDetail);
                vm.SearchText = "%_';--";
                vm.TimelineCategory = vm.FilterOptions.Single(o => o.Value == InventoryCategory.Software);
                Pump(vm.SearchCommand.ExecuteAsync(null));
                var row = Assert.Single(vm.TimelineRows);
                vm.SelectedTimelineRow = row;
                Pump(vm.OpenTimelineCommand.ExecutionTask!);
                Assert.NotNull(vm.TimelineDetail);
                Assert.Contains(vm.TimelineDetail.Fields, f => f.Before == "0" && f.After == "1");
                vm.BeforeSnapshot = vm.SnapshotChoices[1]; vm.AfterSnapshot = vm.SnapshotChoices[0];
                Assert.True(vm.CompareCommand.CanExecute(null));
                Pump(vm.CompareCommand.ExecuteAsync(null));
                Assert.Equal(40, vm.ComparisonRows.Count);
                vm.SelectedComparisonRow = vm.ComparisonRows[0];
                Assert.NotNull(vm.ComparisonDetail);
                var pair = (vm.BeforeSnapshot.Header.Id, vm.AfterSnapshot.Header.Id);
                var detailId = vm.TimelineDetail.Evidence.Id;
                vm.IncidentDate = HistoryReaderTests.Time.ToLocalTime().Date;
                vm.IncidentTime = "invalid";
                Pump(vm.SearchTroubleCommand.ExecuteAsync(null));
                Assert.Equal(appearance.Text("InvalidIncidentTime"), vm.ErrorMessage);
                vm.ToggleLanguageCommand.Execute(null);
                Assert.Equal(appearance.Text("InvalidIncidentTime"), vm.ErrorMessage);
                vm.ToggleLanguageCommand.Execute(null);
                vm.IncidentTime = HistoryReaderTests.Time.AddMinutes(1).ToLocalTime().ToString("HH:mm:ss");
                vm.TroubleCategory = vm.FilterOptions.Single(o => o.Value == InventoryCategory.Software);
                Pump(vm.SearchTroubleCommand.ExecuteAsync(null));
                Assert.Equal(40, vm.TroubleRows.Count);
                Pump(vm.OpenTroubleCommand.ExecuteAsync(vm.TroubleRows[0]));
                Assert.NotNull(vm.TroubleDetail);
                var window = new MainWindow(vm) { Width = 960, Height = 620, ShowInTaskbar = false };
                window.Show();
                try
                {
                    foreach (var language in new[] { "en-US", "zh-CN" })
                    {
                        if (appearance.Language != language) vm.ToggleLanguageCommand.Execute(null);
                        Assert.Equal("%_';--", vm.SearchText);
                        Assert.Equal(InventoryCategory.Software, vm.TimelineCategory!.Value);
                        Assert.Equal(detailId, vm.TimelineDetail!.Evidence.Id);
                        Assert.Equal(pair, (vm.BeforeSnapshot!.Header.Id, vm.AfterSnapshot!.Header.Id));
                        Assert.NotNull(vm.ComparisonDetail);
                        foreach (var dark in new[] { true, false })
                        {
                            if (appearance.IsDark != dark) vm.ToggleThemeCommand.Execute(null);
                            for (var page = 0; page < 5; page++)
                            {
                                vm.SelectedPageIndex = page;
                                Layout(window);
                                var rendered = Descendants(window).OfType<TextBlock>().Select(t => t.Text).ToArray();
                                Assert.DoesNotContain(rendered, text => text.Contains("DEMO", StringComparison.OrdinalIgnoreCase));
                                Assert.DoesNotContain(rendered, text => text.Contains("CategoryOption {") || text.Contains("WindowOption {") || text.Contains("SnapshotOption {"));
                                var title = new[] { "OverviewTitle", "TimelineTitle", "SnapshotsTitle", "TroubleshootTitle", "SettingsTitle" }[page];
                                Assert.Contains(appearance.Text(title), rendered);
                                Assert.True(Descendants(window).OfType<UserControl>().All(v => v.ActualWidth > 0));
                                foreach (var scale in new[] { 1d, 1.5d, 2d })
                                    SavePreview(window, $"{language}-{(dark ? "dark" : "light")}-{page}-{(int)(scale * 100)}", scale);
                            }
                        }
                    }
                    Assert.Equal(3, app.Resources.MergedDictionaries.Count);
                    window.Width = 1180; window.Height = 780;
                    if (!appearance.IsDark) vm.ToggleThemeCommand.Execute(null);
                    foreach (var page in new[] { 0, 1, 2, 3 })
                    {
                        vm.SelectedPageIndex = page; Layout(window);
                        SavePreview(window, $"normal-zh-dark-{page}", 1);
                    }
                    // Empty history uses the same production pages and creates no store.
                    var empty = new MainViewModel(new EmptySession(), new ViewModelTests.EmptyReader(), appearance, NullLogger<MainViewModel>.Instance);
                    Pump(empty.InitializeAsync());
                    window.DataContext = empty;
                    for (var page = 0; page < 5; page++)
                    {
                        empty.SelectedPageIndex = page; Layout(window);
                        SavePreview(window, $"empty-zh-dark-{page}", 1);
                    }
                    window.DataContext = vm;
                    vm.SelectedPageIndex = 2; Layout(window);
                    var categoryPicker = Descendants(window).OfType<ComboBox>().Single(c => c.ItemsSource == vm.SnapshotCategories);
                    categoryPicker.IsDropDownOpen = true; Layout(window);
                    Assert.IsType<ComboBoxItem>(categoryPicker.ItemContainerGenerator.ContainerFromIndex(0));
                    categoryPicker.IsDropDownOpen = false; Layout(window);
                    // Actual WPF templates contain bounded scrolling lists, no production demo switch.
                    vm.SelectedPageIndex = 1; Layout(window);
                    var list = Descendants(window).OfType<ListBox>().Single(l => l.ItemsSource == vm.TimelineRows);
                    Assert.True(VirtualizingPanel.GetIsVirtualizing(list));
                    var search = Descendants(window).OfType<TextBox>().First(t => t.Text == vm.SearchText);
                    Assert.True(search.Focus());
                    Assert.True(search.IsKeyboardFocused);
                }
                finally { window.Close(); }
                listener.Flush();
                Assert.True(string.IsNullOrWhiteSpace(bindingErrors.ToString()), bindingErrors.ToString());
            }
            finally
            {
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
                listener.Dispose(); app.Shutdown();
            }
        });
    }

    private static void Layout(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
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
        new(new EmptySession(), new ViewModelTests.EmptyReader(), appearance, NullLogger<MainViewModel>.Instance);

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

    private sealed class EmptySession : ISystemScanSession
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

    private static void SavePreview(Window window, string name, double scale = 1)
    {
        var folder = Environment.GetEnvironmentVariable("SYSTEMTRACE_QA_DIR");
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        var content = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)(content.ActualWidth * scale), (int)(content.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
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
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                action();
            }
            catch (Exception exception) { failure = exception; }
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "WPF test timed out");
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
