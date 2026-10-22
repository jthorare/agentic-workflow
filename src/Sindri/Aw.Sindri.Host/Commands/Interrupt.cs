using System.Runtime.InteropServices;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// Ctrl-C and SIGTERM as a cancelled token, for a verb that runs until it is told to stop ('workflow events --follow') or that holds
/// resources a kill would leave behind ('onyx up' and 'onyx down' run docker children and hold the workspace lock), so it ends with
/// its own exit code instead of the runtime killing the process. Every other verb keeps the default
/// behaviour (Ctrl-C ends it, also at a prompt that is waiting for typed text), so only a verb that
/// <see cref="Streams"/> listens.
/// </summary>
internal sealed class Interrupt : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    private readonly CancellationTokenSource _again = new();
    private readonly PosixSignalRegistration? _terminate;
    private readonly bool _listening;
    private readonly bool _jobRun;

    private Interrupt(bool listen, bool jobRun)
    {
        _listening = listen;
        _jobRun = jobRun;
        if (!listen)
        {
            return;
        }

        Console.CancelKeyPress += OnCancelKey;
        _terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnTerminate);
    }

    /// <summary>Cancelled by Ctrl-C or SIGTERM; never cancelled for a verb that doesn't stream.</summary>
    public CancellationToken Token => _source.Token;

    /// <summary>Cancelled by the second Ctrl-C of a verb that follows a job (<see cref="FollowsJob"/>): it leaves at once. Never cancelled otherwise.</summary>
    public CancellationToken Again => _again.Token;

    /// <summary>Whether <paramref name="args"/> is a verb that runs until interrupted: 'workflow events … --follow', 'status --follow', 'onyx up' or 'onyx down'.</summary>
    /// <param name="args">The command line.</param>
    /// <returns>True for a following verb.</returns>
    public static bool Streams(string[] args) =>
        FollowsJob(args) || (args is ["workflow", "events", .. var rest] && rest.Contains("--follow", StringComparer.Ordinal))
        || (args is ["status", .. var follow] && follow.Contains("--follow", StringComparer.Ordinal))
        || args is ["onyx", "up" or "down", ..];

    /// <summary>Whether <paramref name="args"/> is 'services run', 'context sources sync', 'context map build', 'context map refresh', 'context eval backtest', 'launch probe' or 'repo add' without --detach: it follows its job, so the first Ctrl-C cancels the job and the second leaves.</summary>
    /// <param name="args">The command line.</param>
    /// <returns>True for a job-following verb.</returns>
    public static bool FollowsJob(string[] args) =>
        !args.Contains("--detach", StringComparer.Ordinal)
        && (args is ["services", "run", ..] or ["context", "sources", "sync", ..] or ["context", "map", "build", ..] or ["context", "map", "refresh", ..] or ["context", "eval", "backtest", ..] or ["launch", "probe", ..] or ["repo", "add", ..]);

    /// <summary>Starts listening when <paramref name="args"/> <see cref="Streams"/> or <see cref="FollowsJob"/>.</summary>
    /// <param name="args">The command line.</param>
    /// <returns>The listener; dispose it to stop listening.</returns>
    public static Interrupt For(string[] args) => new(Streams(args), FollowsJob(args));

    /// <summary>Cancels <see cref="Token"/> as a Ctrl-C does (tests: a signal can't be sent to the test host).</summary>
    internal void Raise() => _source.Cancel();

    public void Dispose()
    {
        if (_listening)
        {
            Console.CancelKeyPress -= OnCancelKey;
            _terminate?.Dispose();
        }

        _source.Dispose();
        _again.Dispose();
    }

    private void OnCancelKey(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;   // the verb ends itself; a second Ctrl-C ends the process the usual way, or the job follower leaves
        if (_source.IsCancellationRequested)
        {
            e.Cancel = _jobRun;
            _again.Cancel();
        }

        _source.Cancel();
    }

    private void OnTerminate(PosixSignalContext context)
    {
        context.Cancel = true;
        _source.Cancel();
    }
}
