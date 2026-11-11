using System.Globalization;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The static helpers of <see cref="GraphAgentLoop"/>: how a session's end reads, the watch and the hold activity.</summary>
internal sealed partial class GraphAgentLoop
{
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
