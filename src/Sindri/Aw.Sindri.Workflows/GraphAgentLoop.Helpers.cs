using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The static helpers of <see cref="GraphAgentLoop"/>: how a session's end reads, the watch and the hold activity.</summary>
internal sealed partial class GraphAgentLoop
{
    // The failure of a session that ended with no report and no outputs: how it ended, then what recovery found, then what the sandbox refused (#3565).
    private static string NoReport(AgentSessionState state, AgentNodeRecovered recovered)
    {
        var why = state.EndedAs switch
        {
            AgentSessionEnd.Lost => "lost: the session stopped heartbeating",
            AgentSessionEnd.Idle => "idle: the agent sat idle at its prompt with no report",
            AgentSessionEnd.Exit or AgentSessionEnd.Limit or null or _ => $"exit: the agent exited (code {state.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}) without reporting",
        };
        var text = recovered.Reason is { } detail ? $"{why}; {detail}" : why;
        return recovered.Denials is { } refused ? $"{text}; {refused}" : text;
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

    // why: the agent's failure output goes through Fit like a succeeded one, so one over 64 KB stays parseable JSON with 'Truncated' set (#3419)
    // The input is fitted too (#3486): it is the interpolated config, and a big prompt must not reach the store as cut, unparseable text.
    private static GraphBlockOutcome Failed(string? input, string error, string? output = null, bool inputCut = false)
    {
        var fitted = StoredJson.Fit(output);
        var fitInput = StoredJson.Fit(input);
        return new(NodeRunStatus.Failed, fitInput.Text, fitted.Text, [], error, Truncated: fitted.Cut || fitInput.Cut || inputCut);
    }

    // What a failed node keeps of the agent's work: the last refused report and the declared output files it wrote. Null when both are empty, so a node that left nothing keeps no output.
    private static string? FailureOutputOf(string? refusedReport, string? files)
    {
        var output = new JsonObject();
        if (!string.IsNullOrEmpty(refusedReport))
        {
            output["lastRefusedReport"] = refusedReport;
        }

        if (files is not null && JsonNode.Parse(files) is JsonObject { Count: > 0 } parsed)
        {
            output["files"] = parsed;
        }

        return output.Count == 0 ? null : output.ToJsonString();
    }

    // The ask activity reads the state file this often and hands back after the cap with nothing new (#3151). Each call is one activity in the run's history, so the cap keeps a long node to a few calls.
    private static readonly TimeSpan _defaultAskEvery = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _defaultAskCap = TimeSpan.FromMinutes(10);

    // After a round of the wait: records an ask that finished, then tells the idle sweep about a takeover or a handback. A handback moves the deadline by the time the node was held.
    private static async Task Settle(AgentWait wait, bool held, TimeSpan remaining, Session session)
    {
        if (session.Poll is { Done: true } finished)
        {
            await finished.Complete().ConfigureAwait(true);
        }

        if (wait.TakenOver == held)
        {
            return;
        }

        await Hold(wait, wait.TakenOver).ConfigureAwait(true);   // the idle sweep is told, and the next round runs the clock or stops it
        if (held)
        {
            await session.Events.MoveDeadline(new DateTimeOffset(Workflow.UtcNow) + remaining).ConfigureAwait(true);   // why: the clock stood still while a person held the node, so the deadline moves by that time
        }
    }

    private sealed record Session(AgentNodeRef Reference, AgentLaunched Launched, NodeAgentRecorder Events, AgentAskPoll? Poll);
}
