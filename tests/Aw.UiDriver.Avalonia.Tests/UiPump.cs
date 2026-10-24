using Avalonia.Headless;
using Aw.UiDriver.Avalonia.Tests;

[assembly: AssemblyFixture(typeof(UiPump))]

namespace Aw.UiDriver.Avalonia.Tests;

/// <summary>
/// Keeps the headless UI thread pumping for the whole test run. The driver reaches the UI thread through Dispatcher.UIThread from other threads, as it
/// does in an app; the headless session only pumps while one of its dispatched jobs is pending, so one job that ends with the run holds the loop open.
/// </summary>
public sealed class UiPump : IAsyncLifetime
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _stop = new();
    private Task _job = Task.CompletedTask;

    public async ValueTask InitializeAsync()
    {
        var running = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _job = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(UiPump).Assembly).Dispatch(
            async () =>
            {
                running.SetResult();
                await _release.Task;
                return true;
            },
            _stop.Token);
        await running.Task.WaitAsync(TimeSpan.FromSeconds(30), _stop.Token);
    }

    public async ValueTask DisposeAsync()
    {
        _ = _release.TrySetResult();
        await _job;
        _stop.Dispose();
    }
}
