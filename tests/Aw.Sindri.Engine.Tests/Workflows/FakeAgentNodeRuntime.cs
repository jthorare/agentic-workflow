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

    /// <summary>The timeout the launch reports; the real one comes from the node's config, whose smallest value is a minute.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Why the launch fails, or null for a launch that starts.</summary>
    public AgentLaunchFailure? Failure { get; init; }

    /// <summary>What recovery answers; nothing by default.</summary>
    public AgentNodeRecovered Recovered { get; set; } = new(Outputs: null, Reason: null);

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

    public Task<AgentNodeLaunched> Launch(AgentNodeRef node, CancellationToken cancellationToken)
    {
        _launches.Enqueue(node);
        var launched = Failure is { } failure
            ? new AgentLaunched(Worktree: null, StartHead: null, failure)
            : new AgentLaunched("/work/web/.claude/worktrees/wf-test", "0123abc", Failure: null, Agent: "claude-code", RunDirectory: "/home/nodes/run");
        return Task.FromResult(new AgentNodeLaunched(launched, """{"repo":"web"}""", Timeout));
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
        return Task.FromResult(new AgentNodeClosed(Tokens: 100, EmptyDiff: false, Commit: "def4567", AgentSession: "agent-session-1"));
    }

    public Task Hold(AgentNodeHeld held, CancellationToken cancellationToken)
    {
        _holds.Enqueue(held.Held);
        return Task.CompletedTask;
    }
}
