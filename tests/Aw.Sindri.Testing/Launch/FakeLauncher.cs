using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Testing.Launch;

/// <summary>
/// An <see cref="ILauncher"/> in memory, idempotent per launch id: a repeated Launch returns the first call's Started, and a
/// <see cref="FailNextWith"/> script makes the next new launch fail. Engine P8 and the launcher service test against it.
/// </summary>
public sealed class FakeLauncher : ILauncher
{
    private readonly Lock _gate = new();   // the launch activity appends on the worker's thread while a test reads Requests and Stops
    private readonly Dictionary<LaunchId, StartedLaunch> _live = [];
    private readonly List<LaunchRequest> _requests = [];
    private readonly List<LaunchId> _stops = [];
    private (Error Error, LaunchStage Stage)? _nextFailure;
    private int _failuresLeft;

    /// <summary>What <see cref="Probe"/> returns.</summary>
    public LaunchProbed Probed { get; set; } = new(
        new TerminalFacts("print", "Print", Present: true, "detected", ConfigPath: null, Problem: null, Detail: null),
        new HostFacts(HostKind.Tmux, Present: true, "3.4", Problem: null), [], []);

    /// <summary>The distinct launches started and not stopped.</summary>
    public int SessionCount => _live.Count;

    /// <summary>Every request Launch received, repeats included, in order (a snapshot).</summary>
    public IReadOnlyList<LaunchRequest> Requests { get { lock (_gate) { return [.. _requests]; } } }

    /// <summary>Every launch id Stop received, repeats included, in order (a snapshot).</summary>
    public IReadOnlyList<LaunchId> Stops { get { lock (_gate) { return [.. _stops]; } } }

    /// <summary>Makes the next new launch fail; a repeat of a live launch still returns its Started.</summary>
    /// <param name="error">The catalogue error.</param>
    /// <param name="stage">Where it stopped.</param>
    /// <param name="times">How many new launches fail before one starts.</param>
    public void FailNextWith(Error error, LaunchStage stage = LaunchStage.Host, int times = 1)
    {
        _nextFailure = (error, stage);
        _failuresLeft = times;
    }

    /// <summary>The session id every new launch reports as chosen before it started, or null (an agent whose id is reported later).</summary>
    public Core.Ids.SessionId? PreChosenSession { get; set; }

    /// <summary>The run directory every new launch reports on its Started, or null (a human open).</summary>
    public string? RunDirectory { get; set; }

    /// <summary>When set, 'Launch' waits for it (and its caller's cancellation) before answering: a launch that is still running.</summary>
    public Task? Hold { get; set; }

    /// <summary>What the viewer did for an attended launch; null opens it.</summary>
    public ViewerOutcome? Viewer { get; set; }

    /// <inheritdoc/>
    public async ValueTask<LaunchResult> Launch(LaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            _requests.Add(request);
        }

        if (Hold is { } hold)
        {
            await hold.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Answer(request);
    }

    private LaunchResult Answer(LaunchRequest request)
    {
        if (_live.TryGetValue(request.Launch, out var existing))
        {
            return existing;
        }

        if (_nextFailure is { } failure)
        {
            if (--_failuresLeft <= 0)
            {
                _nextFailure = null;
            }

            return new FailedLaunch(failure.Error, failure.Stage);
        }

        var host = new HostRef(HostKind.Tmux, LaunchSessionName.For(request.Launch));
        var started = new StartedLaunch(
            host, request.Attended ? Viewer ?? ViewerOutcome.Opened : ViewerOutcome.Skipped,
            ["tmux", "-L", "sindri-launch", "attach", "-t", "=" + host.Name], CatalogSource.Builtin, "fake-argv-hash")
        {
            ChosenSession = PreChosenSession?.Value,
            RunDirectory = RunDirectory,
        };
        _live[request.Launch] = started;
        return started;
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Stop(LaunchId launch, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _stops.Add(launch);
        }

        _ = _live.Remove(launch);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    /// <inheritdoc/>
    public ValueTask<LaunchProbed> Probe(CancellationToken cancellationToken) => ValueTask.FromResult(Probed);
}
