namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// The control plane follow a Shell test starts (HealthViewModel.Run), and its teardown: cancel it, then check that it ended
/// (#4439). Cancelling never ends the follow inside Cancel itself: its awaits come back through the thread pool.
/// </summary>
internal sealed class FollowLoop : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private Task _run = Task.CompletedTask;

    /// <summary>Starts the follow with this loop's token.</summary>
    /// <param name="run">The follow, given the token that stops it.</param>
    internal void Start(Func<CancellationToken, Task> run) => _run = run(_stop.Token);

    /// <summary>Cancels the follow and checks that it ended.</summary>
    /// <returns>A task that ends once the follow did.</returns>
    public ValueTask DisposeAsync()
    {
#pragma warning disable MA0042 // #4439 red: today's teardown, a blocking 2 s wait
        _stop.Cancel();
        Assert.True(_run.Wait(TimeSpan.FromSeconds(2), CancellationToken.None), "Run ended when cancelled");
#pragma warning restore MA0042
        _stop.Dispose();
        return ValueTask.CompletedTask;
    }
}
