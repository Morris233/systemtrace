using Microsoft.Extensions.Logging.Abstractions;
using SystemTrace.App.Services;
using SystemTrace.App.ViewModels;
using SystemTrace.Core;
using Xunit;

namespace SystemTrace.Tests;

public sealed class ViewModelTests
{
    [Fact]
    public async Task IoFailureIsRecoverableAndDoesNotReplaceHistory()
    {
        var vm = Create(new FailingSession());
        await vm.RunDemoCommand.ExecuteAsync(null);
        Assert.Equal("StorageError", vm.ErrorMessage);
        Assert.Equal(0, vm.History.TotalRuns);
        Assert.False(vm.IsLoading);
        Assert.True(vm.RunDemoCommand.CanExecute(null));
    }

    [Fact]
    public async Task PendingCommandDisablesReentryAndCancellationRestoresIdle()
    {
        var session = new WaitingSession();
        var vm = Create(session);
        var task = vm.RunDemoCommand.ExecuteAsync(null);
        await session.Started.Task;
        Assert.False(vm.RunDemoCommand.CanExecute(null));
        vm.RunDemoCommand.Cancel();
        await task;
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsLoading);
        Assert.True(vm.RunDemoCommand.CanExecute(null));
        vm.CancelPending();
        Assert.False(vm.RunDemoCommand.CanExecute(null));
    }

    private static MainViewModel Create(IDemoSession session) => new(session, new Appearance(), NullLogger<MainViewModel>.Instance);
    private sealed class Appearance : IAppearanceService
    {
        public string Language => "en-US";
        public bool IsDark => true;
        public string Text(string key) => key;
        public void ToggleLanguage() { }
        public void ToggleTheme() { }
    }
    private sealed class FailingSession : IDemoSession
    {
        public Task<InventoryHistory> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(InventoryHistory.Empty);
        public Task<InventoryHistory> RunAsync(CancellationToken cancellationToken = default) => throw new System.IO.IOException("Simulated I/O failure");
    }
    private sealed class WaitingSession : IDemoSession
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<InventoryHistory> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(InventoryHistory.Empty);
        public async Task<InventoryHistory> RunAsync(CancellationToken cancellationToken = default)
        {
            Started.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return InventoryHistory.Empty;
        }
    }
}
