using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The agent loop of a 'run_agent' node (PR 7), for the run's walk (item 0) and for a 'for_each' batch (one agent per item, #2470): launch the session, wait for it with a heartbeating activity raced against the node's timeout, take the report (or read the output folder of a session that ended without
/// one), close the run and hand on the declared outputs plus status, branch, commit, sessionId and usage. A takeover pauses the timeout and a handback resumes what was left of it.
/// </summary>
/// <param name="run">The run.</param>
/// <param name="launchAttempts">How many times a failing launch is tried before the node fails; 0 or less for <see cref="DefaultAgentLaunchAttempts"/>.</param>
/// <param name="announce">Told when the node starts waiting on a session and when it stops (null): a batch workflow passes it on to the run workflow; the run's own walk passes none.</param>
internal sealed partial class GraphAgentLoop(WorkflowRunId run, int launchAttempts, Func<AgentWait?, Task>? announce = null)
{
    // why: a launch hands back after its slice when the session cap is full, and the workflow asks again. A cancel never goes to the launch (it does not heartbeat, so it could not hear one):
    // the workflow lets the launch return, then stops, so the close that follows sees the session the launch made (TryCancel let the close run first and left an orphan). Sending the cancel
    // anyway raced the launch's completion, and the server refused the whole workflow task ("ACTIVITY_UNKNOWN for scheduledEventId"). The one-shot reads and the close are retried without end like the writes
    private static readonly ActivityOptions _agentLaunch = new() { StartToCloseTimeout = TimeSpan.FromMinutes(10), RetryPolicy = GraphActivityOptions.Unbounded, CancellationToken = CancellationToken.None };
    private static readonly ActivityOptions _agentLaunchAttempt = new() { StartToCloseTimeout = TimeSpan.FromMinutes(10), RetryPolicy = new Temporalio.Common.RetryPolicy { MaximumAttempts = 1 }, CancellationToken = CancellationToken.None };
    private static readonly ActivityOptions _agentRead = new() { StartToCloseTimeout = TimeSpan.FromMinutes(5), RetryPolicy = GraphActivityOptions.Unbounded };
    private static readonly ActivityOptions _detached = GraphActivityOptions.Detached;

    private readonly WorkflowRunId _run = run;
    private readonly int _agentLaunchAttempts = launchAttempts > 0 ? launchAttempts : DefaultAgentLaunchAttempts;

    /// <summary>The node this workflow waits on, or null.</summary>
    public AgentWait? Current { get; private set; }

    /// <summary>Runs the node's agent for one item.</summary>
    /// <param name="node">The 'run_agent' node.</param>
    /// <param name="itemIndex">The item's position counted from 1, or 0 for a node that runs once.</param>
    /// <returns>The node's outcome.</returns>
    public async Task<GraphBlockOutcome> Run(GraphNode node, int itemIndex)
    {
        var reference = new AgentNodeRef(_run, node.Id, itemIndex);
        var address = NodeRunAddress.Of(_run, node.Id, itemIndex);
        var launch = address.Launch().Value;
        var events = new NodeAgentRecorder(_run, node.Id, itemIndex, Workflow.Patched("node-agent-events-1")) { Asks = Workflow.Patched("node-agent-ask-1") };
        events.Launched(address.Launch(), address.WorkBranch);
        try
        {
            AgentNodeLaunched started;
            try
            {
                started = await Launch(reference, events).ConfigureAwait(true);
            }
            catch (ActivityFailureException e) when (!TemporalException.IsCanceledException(e))
            {
                await events.Record(NodeAgentStates.EndedFailed).ConfigureAwait(true);
                return Failed(input: null, e.InnerException?.Message ?? e.Message);   // why: the last attempt's error, as a node whose block threw ends (RunBlock); the node's on_failure then applies as for any failed node (#2578)
            }

            if (started.Launched.Failure is { } failure)
            {
                await events.Record(NodeAgentStates.EndedFailed).ConfigureAwait(true);
                return Failed(started.Input, failure.Text);
            }

            return await Supervise(new AgentWait(node.Id, itemIndex, launch), started, address, reference, events).ConfigureAwait(true);
        }
        catch (Exception e) when (TemporalException.IsCanceledException(e))
        {
            // why: a cancel stops the session, and the stop is written detached so the cancel does not cancel it; there is nothing to measure
            _ = await Workflow.ExecuteActivityAsync((AgentNodeActivities a) => a.CloseAsync(new AgentCloseRun(launch, Worktree: null, StartHead: null, Measure: false)), _detached).ConfigureAwait(true);
            await events.RecordCancelled().ConfigureAwait(true);   // why: the series would end on 'working' otherwise; written detached like the close
            throw;
        }
        finally
        {
            await Announce(wait: null).ConfigureAwait(true);
        }
    }

    /// <summary>What the 'agent-node' query answers while this loop waits on a session.</summary>
    /// <returns>The node, its launch and whether a person holds it, or null.</returns>
    public AgentNodeView? View() => Current is { } wait ? new AgentNodeView(wait.Node.Value, wait.ItemIndex, wait.Launch, wait.TakenOver) : null;

    /// <summary>Takes the node over.</summary>
    /// <param name="call">The node.</param>
    /// <returns>True when this call took it over, false when it already was.</returns>
    public bool TakeOver(AgentNodeCall call)
    {
        var wait = Waiting(call.Node, call.ItemIndex);
        var changed = !wait.TakenOver;
        wait.TakenOver = true;
        return changed;
    }

    /// <summary>Hands the node back.</summary>
    /// <param name="call">The node.</param>
    public void HandBack(AgentNodeCall call) => Waiting(call.Node, call.ItemIndex).TakenOver = false;

    /// <summary>Refuses a handback of a node nobody holds.</summary>
    /// <param name="call">The node.</param>
    public void ValidateHandback(AgentNodeCall call)
    {
        if (!Waiting(call.Node, call.ItemIndex).TakenOver)
        {
            throw Refusal($"node '{call.Node}' is not taken over, so there is nothing to hand back");
        }
    }

    /// <summary>Records the node's report.</summary>
    /// <param name="report">The node and the checked values.</param>
    public void Deliver(AgentNodeDelivery report) => Waiting(report.Node, report.ItemIndex).Report = report;

    /// <summary>Refuses a report for a node not waited on, a second report, and the launched session's report of a node a person holds.</summary>
    /// <param name="report">The node and the checked values.</param>
    public void ValidateReport(AgentNodeDelivery report)
    {
        var wait = Waiting(report.Node, report.ItemIndex);
        if (wait.Report is not null)
        {
            throw Refusal($"node '{report.Node}' was already reported");
        }

        if (wait.TakenOver && !report.Human)
        {
            throw Refusal($"node '{report.Node}' is taken over: only the human reports it (fix: report it from a terminal or the Sindri app, or hand it back)");
        }
    }

    /// <summary>Refuses a takeover of a node not waited on.</summary>
    /// <param name="call">The node.</param>
    public void ValidateTakeover(AgentNodeCall call) => _ = Waiting(call.Node, call.ItemIndex);

    private AgentWait Waiting(string node, int itemIndex) =>
        Current is { } wait && string.Equals(wait.Node.Value, node, StringComparison.Ordinal) && wait.ItemIndex == itemIndex
            ? wait
            : throw Refusal($"the run is not waiting on a run_agent node '{node}'");

    private static ApplicationFailureException Refusal(string message) => new(message, nonRetryable: true);

    private async Task Announce(AgentWait? wait)
    {
        Current = wait;
        if (announce is not null)
        {
            await announce(wait).ConfigureAwait(true);
        }
    }

    private async Task<GraphBlockOutcome> Supervise(AgentWait wait, AgentNodeLaunched started, NodeRunAddress address, AgentNodeRef reference, NodeAgentRecorder events)
    {
        await Announce(wait).ConfigureAwait(true);
        events.DeadlineAt(new DateTimeOffset(Workflow.UtcNow) + started.Timeout);
        await events.Record(NodeAgentStates.Working).ConfigureAwait(true);
        using var stop = new CancellationTokenSource();
        var watch = Workflow.ExecuteActivityAsync((AgentNodeActivities a) => a.WatchAsync(wait.Launch), WatchOptions(stop.Token));
        AgentNodeEnding ending;
        var poll = events.Asks ? new AgentAskPoll(events, started.AskEvery ?? _defaultAskEvery, started.AskCap ?? _defaultAskCap) : null;
        try
        {
            ending = await WaitForAgent(wait, watch, started.Timeout, new Session(reference, started.Launched, events, poll)).ConfigureAwait(true);
        }
        finally
        {
            await (poll?.Stop() ?? Task.CompletedTask).ConfigureAwait(true);

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

        var closed = await Workflow.ExecuteActivityAsync((AgentNodeActivities a) => a.CloseAsync(new AgentCloseRun(wait.Launch, started.Launched.Worktree, started.Launched.StartHead, Measure: true)), _agentRead).ConfigureAwait(true);
        await events.Record(NodeAgentRecorder.EndState(ending.Failure)).ConfigureAwait(true);
        if (ending.Failure is { } reason)
        {
            return Failed(started.Input, reason, ending.FailureOutput);
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

        // why: Fit keeps an output over 64 KB as parseable JSON; the walk then records the node with 'Truncated' set (#3419)
        var fitted = StoredJson.Fit(output.ToJsonString());
        return new GraphBlockOutcome(NodeRunStatus.Succeeded, input, fitted.Text, result.Taken, Error: null, Truncated: fitted.Cut);
    }

    // Waits for the report, the end of the session or the timeout, whichever comes first. The timeout runs only while nobody holds the node: a takeover stops its clock and a handback
    // starts it again with what was left.
    private static async Task<AgentNodeEnding> WaitForAgent(AgentWait wait, Task<AgentSessionState> watch, TimeSpan timeout, Session session)
    {
        var remaining = timeout;
        while (true)
        {
            var held = wait.TakenOver;
            if (!held && remaining <= TimeSpan.Zero)
            {
                return TimedOut(timeout);
            }

            await (session.Poll?.Sync(wait, held) ?? Task.CompletedTask).ConfigureAwait(true);   // why: a permission prompt shows only in the fleet state file, so while the clock runs one long call watches it (#3151)

            var began = Workflow.UtcNow;
            var met = await Round(wait, watch, held, remaining, session.Poll).ConfigureAwait(true);
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
                return await Ended(watch, session.Reference, session.Launched).ConfigureAwait(true);
            }

            if (!met)
            {
                return TimedOut(timeout);
            }

            await Settle(wait, held, remaining, session).ConfigureAwait(true);
        }
    }

    // One round of the wait: until a report, the session's end a change of the permission prompt or of who holds the node; with the clock running (nobody holds it) also until the time left is up. False when the time ran out.
    private static async Task<bool> Round(AgentWait wait, Task<AgentSessionState> watch, bool held, TimeSpan remaining, AgentAskPoll? poll)
    {
        bool Ready() => wait.Report is not null || watch.IsCompleted || wait.TakenOver != held || poll?.Done == true;
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
            return new AgentNodeEnding(Outputs: null, failure, state.Refused ? FailureOutputOf(state.Detail, files: null) : null);
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
        return new AgentNodeEnding(Outputs: null, recovered.Reason is { } detail ? $"{why}; {detail}" : why, FailureOutputOf(refusedReport: null, recovered.Files));
    }
}
