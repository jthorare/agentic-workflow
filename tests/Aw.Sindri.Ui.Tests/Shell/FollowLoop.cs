using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// The control plane follow a Shell test starts (HealthViewModel.Run), and its teardown: cancel it, then await its end (#4439). Cancelling never ends the follow inside Cancel itself: its awaits come back through the thread pool.
/// </summary>
internal sealed class FollowLoop : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    // The only real-time limit is the one every unit test already has (LANE-TEST-001): a starved thread pool (heavy runs the lane 4-wide) can
    // take seconds to resume Run, and a teardown that fails sooner would fail a test the lane cap lets pass.
    private static readonly TimeSpan _endBound = Lanes.BudgetOf(TestLane.Unit);

    private Task _run = Task.CompletedTask;

    /// <summary>Starts the follow with this loop's token.</summary>
    /// <param name="run">The follow, given the token that stops it.</param>
    internal void Start(Func<CancellationToken, Task> run) => _run = run(_stop.Token);

    /// <summary>Cancels the follow, waits for it to end without blocking a thread, and fails if it outlasts the bound.</summary>
    /// <returns>A task that ends once the follow did.</returns>
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        var ended = true;
        try
        {
            await _run.WaitAsync(_endBound, RealTime.Clock, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            ended = false;
        }

        Assert.True(ended, "Run ended when cancelled");
        _stop.Dispose();
    }
}
