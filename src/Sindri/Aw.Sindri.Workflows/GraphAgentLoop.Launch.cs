using Aw.Sindri.Core.Graph;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The launch of <see cref="GraphAgentLoop"/>: asks again while the session cap is full, and retries a launch that throws up to the run's cap.</summary>
internal sealed partial class GraphAgentLoop
{
    // why: a full session cap makes the activity hand back after its slice, and the workflow asks again
    private async Task<AgentNodeLaunched> Launch(AgentNodeRef reference, NodeAgentRecorder events)
    {
        var attempts = Workflow.Patched("agent-launch-attempts-1");   // why: histories from before this change keep the one unbounded retried call
        AgentNodeLaunched started;
        do
        {
            started = attempts
                ? await LaunchOnce(reference).ConfigureAwait(true)
                : await Workflow.ExecuteActivityAsync((AgentNodeActivities a) => a.LaunchAsync(reference), _agentLaunch).ConfigureAwait(true);
            Workflow.CancellationToken.ThrowIfCancellationRequested();   // why: the launch is not cancelled with the workflow (_agentLaunch); a cancel that came during it stops here, and the catch closes what it made
            if (started.Launched.Waiting)
            {
                await events.Record(NodeAgentStates.WaitingForSlot, NodeAgentRecorder.SlotWait).ConfigureAwait(true);
            }
        }
        while (started.Launched.Waiting);

        await events.Record(NodeAgentStates.Launching).ConfigureAwait(true);
        return started;
    }

    // One launch attempt per activity call, retried here: the launch is not cancelled with the workflow (it raced its own completion, #2469), so a launch that keeps throwing
    // is only stoppable between attempts. After each failure the workflow checks for a cancel, and the back-off timer ends on one.
    // After the cap the last error leaves, and RunAgent turns it into a failed node. The cap is the history's bound: each attempt adds a fixed number of events (the activity, its task and the back-off timer),
    // and Temporal ends a workflow at 51,200 events with no ledger record. ~1 h of attempts at the 30 s back-off is 120 attempts, plus the ramp from 100 ms (about 9 attempts in the first minute).
    internal const int DefaultAgentLaunchAttempts = 130;

    private async Task<AgentNodeLaunched> LaunchOnce(AgentNodeRef reference)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await Workflow.ExecuteActivityAsync((AgentNodeActivities a) => a.LaunchAsync(reference), _agentLaunchAttempt).ConfigureAwait(true);
            }
            catch (ActivityFailureException)
            {
                // why: Patched runs only at the cap, so a history shorter than the cap gets no marker. A history that already went past the cap (a launch that failed for over an hour before this change) holds a back-off timer here, so on its replay Patched is false and it keeps retrying as before; failing the node there would not match that history (nondeterminism, #2578)
                if (attempt + 1 >= _agentLaunchAttempts && Workflow.Patched("agent-launch-cap-1"))
                {
                    throw;
                }

                Workflow.CancellationToken.ThrowIfCancellationRequested();
                var backoff = TimeSpan.FromMilliseconds(Math.Min(30_000, 100 * Math.Pow(2, Math.Min(attempt, 10))));
                await Workflow.DelayAsync(backoff, Workflow.CancellationToken).ConfigureAwait(true);
            }
        }
    }
}
