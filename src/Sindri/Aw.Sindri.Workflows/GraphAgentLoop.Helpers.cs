using System.Globalization;
using Aw.Sindri.Core.Graph;
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

    private static readonly TimeSpan _defaultAskEvery = TimeSpan.FromSeconds(15);

    private static readonly ActivityOptions _ask = new() { StartToCloseTimeout = TimeSpan.FromMinutes(1), RetryPolicy = new Temporalio.Common.RetryPolicy { MaximumAttempts = 1 } };

    private sealed record Session(AgentNodeRef Reference, AgentLaunched Launched, AskPoll? Poll);

    // Reads whether the session waits on a permission prompt and records the change (#3151). A read that fails is no ask: the node goes on as working.
    private sealed class AskPoll(NodeAgentRecorder events, TimeSpan every)
    {
        public TimeSpan Every { get; } = every;

        public static TimeSpan Slice(AskPoll? poll, TimeSpan remaining) => poll is not null && poll.Every < remaining ? poll.Every : remaining;

        // False when no time is left (the node timed out); otherwise reads the ask and the wait goes on. 'remaining' already has the slice just waited taken off.
        public static async Task<bool> Next(AskPoll? poll, AgentWait wait, TimeSpan remaining)
        {
            if (poll is null || remaining <= TimeSpan.Zero)
            {
                return false;
            }

            await poll.Read(wait).ConfigureAwait(true);
            return true;
        }

        public async Task Read(AgentWait wait)
        {
            NodeSessionAsk? ask;
            try
            {
                ask = await Workflow.ExecuteActivityAsync((AgentNodeActivities a) => a.AskingAsync(wait.Launch), _ask).ConfigureAwait(true);
            }
            catch (ActivityFailureException e) when (!TemporalException.IsCanceledException(e))
            {
                ask = null;
            }

            await (ask is null ? events.Record(NodeAgentStates.Working) : events.Record(NodeAgentStates.WaitingOnYou, "permission prompt", ask)).ConfigureAwait(true);
        }
    }
}
