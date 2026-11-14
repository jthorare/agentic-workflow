using System.Collections.Concurrent;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// What the agent-node activities do, scripted by the test (PR 7): the launch answers with a worktree, the watch waits until the test ends the session (heartbeating, so a workflow cancel reaches it),
/// recovery and close answer what the test set. Everything that was asked is recorded.
/// </summary>
internal sealed class FakeAgentNodeRuntime : IAgentNodeRuntime
{
    private readonly TaskCompletionSource<AgentSessionState> _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _watching = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentQueue<AgentNodeRef> _launches = new();
    private readonly ConcurrentQueue<AgentCloseRun> _closes = new();
    private readonly ConcurrentQueue<bool> _holds = new();
    private readonly TaskCompletionSource _launching = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentQueue<string> _order = new();
    private int _asked;
    private volatile NodeSessionAsk? _ask;

    /// <summary>The timeout the launch reports; the real one comes from the node's config, whose smallest value is a minute.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>How many launch calls answer "the session cap is full, ask again" before one starts (#3151).</summary>
    public int SlotWaits { get; init; }

    /// <summary>How often an ask call reads the ask; short, so a test does not wait 2 s (#3151).</summary>
    public TimeSpan AskEvery { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>How long one ask call runs with nothing new before it hands back (#3151); the real default is 10 minutes.</summary>
    public TimeSpan AskCap { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Makes the session wait on a permission prompt for <paramref name="tool"/>, or stop waiting (null) (#3151).</summary>
    /// <param name="tool">The tool, or null for no ask.</param>
    public void SetAsk(string? tool) => _ask = tool is null ? null : new NodeSessionAsk(tool, new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero));

    /// <summary>How many times the workflow asked about the permission prompt (#3151).</summary>
    public int AskCalls => Volatile.Read(ref _asked);

    /// <summary>The interpolated config the launch reports as the node's input.</summary>
    public string Input { get; init; } = """{"repo":"web"}""";

    /// <summary>Why the launch fails, or null for a launch that starts.</summary>
    public AgentLaunchFailure? Failure { get; init; }

    /// <summary>What recovery answers; nothing by default.</summary>
    public AgentNodeRecovered Recovered { get; set; } = new(Outputs: null, Reason: null);

    /// <summary>When set, a launch starts, signals <see cref="Launching"/> and waits for this gate before it returns, so a test can cancel while the launch is open.</summary>
    public TaskCompletionSource? LaunchGate { get; init; }

    /// <summary>When set, every launch attempt throws this after signalling <see cref="Launching"/>, like a store error in the real launch.</summary>
    public Exception? LaunchThrows { get; init; }

    /// <summary>Completes when a launch has started (before it waits on <see cref="LaunchGate"/>).</summary>
    public Task Launching => _launching.Task;

    /// <summary>The order of launch returns and closes: "launch-returned", then "close".</summary>
    public IReadOnlyList<string> Order => [.. _order];

    /// <summary>Completes when the node's watch has started.</summary>
    public Task Watching => _watching.Task;

    /// <summary>The launches asked for.</summary>
    public IReadOnlyList<AgentNodeRef> Launches => [.. _launches];

    /// <summary>The closes asked for.</summary>
    public IReadOnlyList<AgentCloseRun> Closes => [.. _closes];

    /// <summary>The marks the idle sweep was told: true for a takeover, false for a handback.</summary>
    public IReadOnlyList<bool> Holds => [.. _holds];

    /// <summary>Ends the session as the watch reports it.</summary>
    /// <param name="state">How it ended.</param>
    public void End(AgentSessionState state) => _ended.TrySetResult(state);

    public async Task<AgentNodeLaunched> Launch(AgentNodeRef node, CancellationToken cancellationToken)
    {
        _launches.Enqueue(node);
        _ = _launching.TrySetResult();
        if (LaunchThrows is { } thrown)
        {
            throw thrown;
        }

        if (LaunchGate is { } gate)
        {
            await gate.Task.ConfigureAwait(false);   // why: deliberately ignores the token, like a real launch in the middle of making a worktree
        }

        if (_launches.Count <= SlotWaits)
        {
            return new AgentNodeLaunched(new AgentLaunched(Worktree: null, StartHead: null, Failure: null, Waiting: true), Input: null, Timeout);
        }

        _order.Enqueue("launch-returned");
        var launched = Failure is { } failure
            ? new AgentLaunched(Worktree: null, StartHead: null, failure)
            : new AgentLaunched("/work/web/.claude/worktrees/wf-test", "0123abc", Failure: null, Agent: "claude-code", RunDirectory: "/home/nodes/run");
        return new AgentNodeLaunched(launched, Input, Timeout, AskEvery, AskCap);
    }

    public async Task<AgentSessionState> Watch(LaunchId launch, Action heartbeat, CancellationToken cancellationToken)
    {
        _ = _watching.TrySetResult();
        while (!_ended.Task.IsCompleted)
        {
            // why: a cancelled Task.Delay completes at once and WhenAny doesn't throw, so without this a cancelled watch spins on heartbeat() for ever
            cancellationToken.ThrowIfCancellationRequested();
            heartbeat();
            _ = await Task.WhenAny(_ended.Task, Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken)).ConfigureAwait(false);
        }

        return await _ended.Task.ConfigureAwait(false);
    }

    public Task<AgentNodeRecovered> Recover(AgentNodeRecover recover, CancellationToken cancellationToken) => Task.FromResult(Recovered);

    public Task<AgentNodeClosed> Close(AgentCloseRun run, CancellationToken cancellationToken)
    {
        _closes.Enqueue(run);
        _order.Enqueue("close");
        return Task.FromResult(new AgentNodeClosed(Tokens: 100, EmptyDiff: false, Commit: "def4567", AgentSession: "agent-session-1"));
    }

    public async Task<NodeSessionAsk?> WatchAsking(AgentNodeAskWatch watch, Action heartbeat, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _asked);
        for (var waited = TimeSpan.Zero; waited < watch.Cap; waited += watch.ReadEvery)
        {
            heartbeat();
            if (!Equals(_ask, watch.Seen))
            {
                return _ask;
            }

            await Task.Delay(watch.ReadEvery, cancellationToken).ConfigureAwait(false);
        }

        return watch.Seen;
    }

    public Task Hold(AgentNodeHeld held, CancellationToken cancellationToken)
    {
        _holds.Enqueue(held.Held);
        return Task.CompletedTask;
    }
}
