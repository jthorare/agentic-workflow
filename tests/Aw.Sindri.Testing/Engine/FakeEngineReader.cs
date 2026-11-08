using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Testing.Engine;

/// <summary>The Temporal engine in memory: the server, worker and mirror a test sets, the running instances, and each instance's history (newest first).</summary>
public sealed class FakeEngineReader : IEngineReader
{
    private readonly Dictionary<string, int> _calls = new(StringComparer.Ordinal);
    private readonly Dictionary<InstanceAddress, (string RunId, IReadOnlyList<EngineEventFacts> Events)> _histories = [];

    /// <summary>The server it reports.</summary>
    public EngineServer ServerFacts { get; set; } = new("temporal-dev", "sindri", 273_600, 50_331_648, "http://127.0.0.1:8233");

    /// <summary>The worker it reports.</summary>
    public EngineWorker WorkerFacts { get; set; } = new(1, "sindri-48211", "sindri", Polling: true, ActivitiesRunning: 2, Backlog: 0, LastHeartbeatSeconds: 1);

    /// <summary>The mirror lag it reports.</summary>
    public EngineMirror MirrorFacts { get; set; } = new(0, 0);

    /// <summary>The running instances.</summary>
    public IList<EngineInstanceFacts> Running { get; } = [];

    /// <summary>When true, every call fails as a server that doesn't answer would (SND-ENGINE-001).</summary>
    public bool Down { get; set; }

    /// <summary>How many times each kind of call ran, by method name.</summary>
    public IDictionary<string, int> Calls => _calls;

    /// <summary>Gives <paramref name="address"/> a history.</summary>
    /// <param name="address">The instance.</param>
    /// <param name="runId">Its run.</param>
    /// <param name="newestFirst">Its events, newest first.</param>
    public void Record(InstanceAddress address, string runId, params EngineEventFacts[] newestFirst) => _histories[address] = (runId, newestFirst);

    /// <inheritdoc/>
    public ValueTask<Result<EngineServer>> Server(CancellationToken cancellationToken) => Answer("Server", ServerFacts);

    /// <inheritdoc/>
    public ValueTask<Result<EngineWorker>> Worker(CancellationToken cancellationToken) => Answer("Worker", WorkerFacts);

    /// <inheritdoc/>
    public ValueTask<Result<EngineMirror>> Mirror(CancellationToken cancellationToken) => Answer("Mirror", MirrorFacts);

    /// <summary>When set, <see cref="Instances"/> waits for it (and ignores its token), so a test holds a read open or never answers.</summary>
    public TaskCompletionSource? Hold { get; set; }

    /// <summary>When set, <see cref="Instances"/> throws it, as a failure that isn't an error result would.</summary>
    public Exception? Throws { get; set; }

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<EngineInstanceFacts>>> Instances(CancellationToken cancellationToken)
    {
        _calls["Instances"] = _calls.GetValueOrDefault("Instances") + 1;
        if (Hold is { } hold)
        {
            await hold.Task.ConfigureAwait(false);
        }

        return Throws is { } failure
            ? throw failure
            : Down ? SindriErrors.EngineUnavailable.With("the fake engine is down") : Results.Success<IReadOnlyList<EngineInstanceFacts>>([.. Running]);
    }

    /// <inheritdoc/>
    public ValueTask<Result<EngineHistoryFacts>> History(string reference, int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        _calls["History"] = _calls.GetValueOrDefault("History") + 1;
        if (Down)
        {
            return ValueTask.FromResult<Result<EngineHistoryFacts>>(SindriErrors.EngineUnavailable.With("the fake engine is down"));
        }

        var found = _histories.Keys.Where(a => a.Value == reference || WorkflowInstanceId.Of(a).Value == reference).Cast<InstanceAddress?>().FirstOrDefault();
        if (found is not { } address)
        {
            return ValueTask.FromResult<Result<EngineHistoryFacts>>(SindriErrors.EngineNotFound.With($"no execution for '{reference}'"));
        }

        var (run, events) = _histories[address];
        return ValueTask.FromResult<Result<EngineHistoryFacts>>(new EngineHistoryFacts(address, run, [.. events.Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize)], events.Count));
    }

    private ValueTask<Result<T>> Answer<T>(string call, T value)
    {
        _calls[call] = _calls.GetValueOrDefault(call) + 1;
        return ValueTask.FromResult<Result<T>>(Down ? SindriErrors.EngineUnavailable.With("the fake engine is down") : value);
    }
}
