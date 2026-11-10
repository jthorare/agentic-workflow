using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// A 'run_agent' node (PR 7): launch the session, wait for it with a heartbeating activity raced against the node's timeout, take the report (or read the output folder of a session that ended without
/// one), close the run and hand on the declared outputs plus status, branch, commit, sessionId and usage. A takeover pauses the timeout and a handback resumes what was left of it.
/// </summary>
internal sealed partial class GraphWorkflow
{
    private async Task<GraphBlockOutcome> RunAgent(GraphNode node)
    {
        var reference = new AgentNodeRef(_run, node.Id, ItemIndex: 0);
        var address = NodeRunAddress.Of(_run, node.Id);
        var launch = address.Launch().Value;
        try
        {
            AgentNodeLaunched started;
            var attempts = Workflow.Patched("agent-launch-attempts-1");   // why: histories from before this change keep the one unbounded retried call
            do
            {
                started = attempts
                    ? await LaunchOnce(reference).ConfigureAwait(true)
                    : await Workflow.ExecuteActivityAsync((AgentNodeActivities a) => a.LaunchAsync(reference), _agentLaunch).ConfigureAwait(true);   // why: a full session cap makes the activity hand back after its slice, and the workflow asks again
                Workflow.CancellationToken.ThrowIfCancellationRequested();   // why: the launch is not cancelled with the workflow (_agentLaunch); a cancel that came during it stops here, and the catch closes what it made
            }
            while (started.Launched.Waiting);

            if (started.Launched.Failure is { } failure)
            {
                return Failed(started.Input, failure.Text);
            }

            return await Supervise(new AgentWait(node.Id, 0, launch), started, address, reference).ConfigureAwait(true);
        }
        catch (Exception e) when (TemporalException.IsCanceledException(e))
        {
            // why: a cancel stops the session, and the stop is written detached so the cancel does not cancel it; there is nothing to measure
            _ = await Workflow.ExecuteActivityAsync((AgentNodeActivities a) => a.CloseAsync(new AgentCloseRun(launch, Worktree: null, StartHead: null, Measure: false)), _detached).ConfigureAwait(true);
            throw;
        }
        finally
        {
            _agent = null;
        }
    }

    // One launch attempt per activity call, retried here: the launch is not cancelled with the workflow (it raced its own completion, #2469), so a launch that keeps throwing
    // is only stoppable between attempts. After each failure the workflow checks for a cancel, and the back-off timer ends on one.
    private static async Task<AgentNodeLaunched> LaunchOnce(AgentNodeRef reference)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await Workflow.ExecuteActivityAsync((AgentNodeActivities a) => a.LaunchAsync(reference), _agentLaunchAttempt).ConfigureAwait(true);
            }
            catch (ActivityFailureException)
            {
                Workflow.CancellationToken.ThrowIfCancellationRequested();
                var backoff = TimeSpan.FromMilliseconds(Math.Min(30_000, 100 * Math.Pow(2, Math.Min(attempt, 10))));
                await Workflow.DelayAsync(backoff, Workflow.CancellationToken).ConfigureAwait(true);
            }
        }
    }

    private async Task<GraphBlockOutcome> Supervise(AgentWait wait, AgentNodeLaunched started, NodeRunAddress address, AgentNodeRef reference)
    {
        _agent = wait;
        var launched = started.Launched;
        using var stop = new CancellationTokenSource();
        var watch = Workflow.ExecuteActivityAsync((AgentNodeActivities a) => a.WatchAsync(wait.Launch), WatchOptions(stop.Token));
        AgentNodeEnding ending;
        try
        {
            ending = await WaitForAgent(wait, watch, started.Timeout, reference, launched).ConfigureAwait(true);
        }
        finally
        {
            // why: the watch has done its job (or the workflow was cancelled); this stops its heartbeats, and TryCancel does not wait for the activity.
            // Cancel, not CancelAsync: CancelAsync runs the callbacks on the thread pool, outside the workflow's scheduler, so a workflow cancel never reached the watch (Temporal .NET docs)
#pragma warning disable MA0042
            stop.Cancel();
#pragma warning restore MA0042
        }

        await Observe(watch).ConfigureAwait(true);

        if (wait.TakenOver)
        {
            await Hold(wait, held: false).ConfigureAwait(true);   // the human's report ended the node while it was held
        }

        var closed = await Workflow.ExecuteActivityAsync((AgentNodeActivities a) => a.CloseAsync(new AgentCloseRun(wait.Launch, launched.Worktree, launched.StartHead, Measure: true)), _agentRead).ConfigureAwait(true);
        if (ending.Failure is { } reason)
        {
            return Failed(started.Input, reason);
        }

        return Succeeded(started.Input, ending.Outputs, address, closed);
    }

    // The agent's reported outputs plus what closing the run measured, as the node's succeeded outcome.
    private static GraphBlockOutcome Succeeded(string? input, string? reported, NodeRunAddress address, AgentNodeClosed closed)
    {
        var values = ((JsonObject?)JsonNode.Parse(reported ?? "{}") ?? []).ToDictionary(static p => p.Key, static p => p.Value?.DeepClone(), StringComparer.Ordinal);
        var result = RunAgentOutput.Compose(values, new AgentNodeEnd(address.WorkBranch, closed.Commit, closed.AgentSession, closed.Tokens, closed.EmptyDiff));
        var output = new JsonObject();
        foreach (var (name, value) in result.Outputs)
        {
            output[name] = value?.DeepClone();
        }

        return new GraphBlockOutcome(NodeRunStatus.Succeeded, input, output.ToJsonString(), result.Taken, Error: null);
    }

    // Waits for the report, the end of the session or the timeout, whichever comes first. The timeout runs only while nobody holds the node: a takeover stops its clock and a handback
    // starts it again with what was left.
    private static async Task<AgentNodeEnding> WaitForAgent(AgentWait wait, Task<AgentSessionState> watch, TimeSpan timeout, AgentNodeRef reference, AgentLaunched launched)
    {
        var remaining = timeout;
        while (true)
        {
            var held = wait.TakenOver;
            if (!held && remaining <= TimeSpan.Zero)
            {
                return TimedOut(timeout);
            }

            var began = Workflow.UtcNow;
            var met = await Round(wait, watch, held, remaining).ConfigureAwait(true);
            if (!held)
            {
                remaining -= Workflow.UtcNow - began;
            }

            if (wait.Report is { } report)
            {
                return new AgentNodeEnding(report.Outputs, Failure: null);
            }

            if (watch.IsCompleted)
            {
                return await Ended(watch, reference, launched).ConfigureAwait(true);
            }

            if (!met)
            {
                return TimedOut(timeout);
            }

            await Hold(wait, wait.TakenOver).ConfigureAwait(true);   // the node was taken over or handed back: the idle sweep is told, and the next round runs the clock or stops it
        }
    }

    // One round of the wait: until a report, the session's end or a change of who holds the node; with the clock running (nobody holds it) also until the time left is up. False when the time ran out.
    private static async Task<bool> Round(AgentWait wait, Task<AgentSessionState> watch, bool held, TimeSpan remaining)
    {
        bool Ready() => wait.Report is not null || watch.IsCompleted || wait.TakenOver != held;
        if (held)
        {
            await Workflow.WaitConditionAsync(Ready).ConfigureAwait(true);
            return true;
        }

        return await Workflow.WaitConditionAsync(Ready, remaining).ConfigureAwait(true);
    }

    // The session ended with no report: an exit, a lost heartbeat or an idle agent may still have left every declared output in its output folder; a sandbox limit or a refused report may not.
    private static async Task<AgentNodeEnding> Ended(Task<AgentSessionState> watch, AgentNodeRef reference, AgentLaunched launched)
    {
        AgentSessionState state;
        try
        {
            state = await watch.ConfigureAwait(true);
        }
        catch (ActivityFailureException e) when (!TemporalException.IsCanceledException(e))
        {
            return new AgentNodeEnding(Outputs: null, $"lost: the watch of the session failed: {e.InnerException?.Message ?? e.Message}");
        }

        if (Unrecoverable(state) is { } failure)
        {
            return new AgentNodeEnding(Outputs: null, failure);
        }

        var recovered = await Workflow.ExecuteActivityAsync((AgentNodeActivities a) => a.RecoverAsync(new AgentNodeRecover(reference, launched.Worktree ?? string.Empty, launched.RunDirectory)), _agentRead).ConfigureAwait(true);
        if (recovered.Outputs is not null)
        {
            return new AgentNodeEnding(recovered.Outputs, Failure: null);
        }

        var why = state.EndedAs switch
        {
            AgentSessionEnd.Lost => "lost: the session stopped heartbeating",
            AgentSessionEnd.Idle => "idle: the agent sat idle at its prompt with no report",
            AgentSessionEnd.Exit or AgentSessionEnd.Limit or null or _ => $"exit: the agent exited (code {state.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}) without reporting",
        };
        return new AgentNodeEnding(Outputs: null, recovered.Reason is { } detail ? $"{why}; {detail}" : why);
    }

    // Why a session that ended can't have finished the node: a report the engine refused it for, or a sandbox limit. Null when its output folder may still prove it did.
    private static string? Unrecoverable(AgentSessionState state) =>
        state.Refused ? $"exit: {state.Detail}"
        : state.EndedAs == AgentSessionEnd.Limit ? $"limit: the sandbox killed the session ({state.Limit?.Detail})"
        : null;

    private static AgentNodeEnding TimedOut(TimeSpan timeout) =>
        new(Outputs: null, string.Create(CultureInfo.InvariantCulture, $"timeout: the node ran past its limit of {timeout.TotalSeconds:0} s (time a person held it does not count)"));

    // The watch ends by its own cancel or by the session's end; either way its outcome was read already or is not wanted, so a cancelled or failed watch is not an error here.
    private static async Task Observe(Task<AgentSessionState> watch)
    {
        try
        {
            _ = await watch.ConfigureAwait(true);
        }
        catch (Exception e) when (TemporalException.IsCanceledException(e) || e is ActivityFailureException)
        {
            // the cancel above, or a failure Ended already turned into the node's error
        }
    }

    private static Task Hold(AgentWait wait, bool held) =>
        Workflow.ExecuteActivityAsync((AgentNodeActivities a) => a.HoldAsync(new AgentNodeHeld(wait.Launch, held)), _agentRead);

    private static ActivityOptions WatchOptions(CancellationToken stop) => new()
    {
        StartToCloseTimeout = TimeSpan.FromDays(30),
        HeartbeatTimeout = TimeSpan.FromMinutes(1),
        RetryPolicy = GraphActivityOptions.Unbounded,
        CancellationToken = stop,
        CancellationType = ActivityCancellationType.TryCancel,
    };

    private static GraphBlockOutcome Failed(string? input, string error) => new(NodeRunStatus.Failed, input, Output: null, [], error);
}
