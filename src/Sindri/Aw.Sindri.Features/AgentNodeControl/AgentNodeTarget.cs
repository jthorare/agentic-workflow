using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.AgentNodeControl;

/// <summary>What a takeover and a handback check before they ask Temporal: the caller is set, the run is known and running, and the run waits on that 'run_agent' node.</summary>
internal static class AgentNodeTarget
{
    internal const string Waiting = "waiting";
    internal const string TakenOver = "taken_over";

    /// <summary>Finds the node the run waits on.</summary>
    /// <param name="callers">The caller: a human or an agent session may take a node over and hand it back (agent-first D2); a request with no caller at all is refused.</param>
    /// <param name="runs">The runs.</param>
    /// <param name="control">Says which node the run waits on.</param>
    /// <param name="ask">The run, the node and the item's position.</param>
    /// <param name="cancellationToken">Cancels the lookups.</param>
    /// <returns>The node's state; SND-AUTH-002, SND-GRAPH-101 (unknown run), SND-GRAPH-107 (ended run), SND-GRAPH-109 (not waiting on that node), SND-API-001 (Temporal down) or invalid input for a negative index.</returns>
    internal static async ValueTask<Result<RunningAgentNode>> Find(ICallerContextAccessor callers, IWorkflowRunStore runs, IAgentNodeControl control, AgentNodeAsk ask, CancellationToken cancellationToken)
    {
        var (run, node, itemIndex) = ask;
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("taking over and handing back an agent node needs a caller");
        }

        if (itemIndex < 0)
        {
            return SindriErrors.InvalidInput.With(string.Create(CultureInfo.InvariantCulture, $"itemIndex is {itemIndex}; it is the item's position, 0 for a node that runs once"));
        }

        var found = await runs.Get(run, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        if (found.Value.Status is WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled)
        {
            return SindriErrors.GraphRunEnded.With($"the run '{run.Value}' already ended, so its agent node has too");
        }

        var running = await control.Running(run, cancellationToken).ConfigureAwait(false);
        if (running.IsError)
        {
            return running.Error;
        }

        return running.Value is { } now && now.Node == node && now.ItemIndex == itemIndex
            ? now
            : SindriErrors.GraphNodeNotWaiting.With(string.Create(CultureInfo.InvariantCulture, $"the run '{run.Value}' is not waiting on an agent at node '{node.Value}' item {itemIndex}"));
    }
}
