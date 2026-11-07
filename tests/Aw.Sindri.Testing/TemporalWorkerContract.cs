using System.Net;
using System.Net.Sockets;
using Aw.Sindri.Core.Control;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="ITemporalWorker"/> honours, which <see cref="TemporalRuntime"/>'s stop order relies on:
/// Run polls until it is stopped, then completes, and it never throws (a worker logs its own fault), whether or not the
/// frontend answers. Run against the test worker and, with the pinned 'temporal' CLI, the SDK worker host.
/// </summary>
public abstract class TemporalWorkerContract : IAsyncLifetime
{
    private static readonly TimeSpan _stopWithin = TimeSpan.FromSeconds(10);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A worker, and a frontend that answers.</summary>
    /// <returns>The worker and the frontend's endpoint.</returns>
    protected abstract ValueTask<(ITemporalWorker Worker, TemporalEndpoint Frontend)> Create();

    /// <inheritdoc/>
    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc/>
    public virtual ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    /// <summary>Polls until stopped, then completes without throwing.</summary>
    /// <returns>A task.</returns>
    [Fact(Timeout = 60_000)]
    public async Task Run_polls_until_it_is_stopped_then_completes_without_throwing()
    {
        var (worker, frontend) = await Create();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = worker.Run(frontend, stop.Token);
        await Task.Delay(300, Token);
        var completedBeforeTheStop = run.IsCompleted;
        await stop.CancelAsync();
        await run.WaitAsync(_stopWithin, TimeProvider.System, Token);   // throws if it threw, or didn't stop in time

        Expect.True(!completedBeforeTheStop);   // why: TemporalRuntime waits for Run as "the worker stopped"
    }

    /// <summary>A frontend that doesn't answer ends the run without an exception.</summary>
    /// <returns>A task.</returns>
    [Fact(Timeout = 60_000)]
    public async Task Run_against_a_frontend_that_does_not_answer_completes_without_throwing_once_stopped()
    {
        var (worker, _) = await Create();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);

        using var frontend = ClosedPort();
        using var ui = ClosedPort();

        var run = worker.Run(new TemporalEndpoint(Pid: 1, Port: PortOf(frontend), UiPort: PortOf(ui)), stop.Token);
        await Task.Delay(300, Token);
        await stop.CancelAsync();

        await run.WaitAsync(_stopWithin, TimeProvider.System, Token);   // why: serve runs on without a worker; its stop must not fault
    }

    /// <summary>A loopback port nothing listens on: bound without Listen(), so it stays ours and a connect is refused until the socket is disposed (#2249).</summary>
    private static Socket ClosedPort()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return socket;
    }

    private static int PortOf(Socket socket) => ((IPEndPoint)socket.LocalEndPoint!).Port;
}
