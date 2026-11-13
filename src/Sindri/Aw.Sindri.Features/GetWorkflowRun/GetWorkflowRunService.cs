using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.GetWorkflowRun;

/// <summary>One run with its node runs; an unknown run is SND-GRAPH-101. A node payload the store cut short comes back as text, with the node's Truncated flag set.</summary>
/// <param name="runs">The runs and node runs.</param>
/// <param name="workflows">The revision the run walked: it says which nodes are 'run_agent' nodes.</param>
/// <param name="files">The per-launch files: the held marker says whether a person holds an agent node, when the run can't be asked.</param>
/// <param name="control">Asks the run's workflow which agent node it waits on and whether it is held: the same query a takeover flips, so a read right after one is current.</param>
internal sealed class GetWorkflowRunService(IWorkflowRunStore runs, IWorkflowStore workflows, ILaunchFiles files, IAgentNodeControl control) : IService<GetWorkflowRunRequest, GetWorkflowRunResponse>
{
    public async ValueTask<Result<GetWorkflowRunResponse>> Handle(GetWorkflowRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var run = await runs.Get(request.Run, cancellationToken).ConfigureAwait(false);
        if (run.IsError)
        {
            return run.Error;
        }

        var nodes = await runs.NodeRuns(request.Run, cancellationToken).ConfigureAwait(false);
        if (nodes.IsError)
        {
            return nodes.Error;
        }

        var found = run.Value;
        var agentNodes = await AgentNodes(found, nodes.Value, cancellationToken).ConfigureAwait(false);
        var slug = WorkflowSlug.From(found.Id.Value[..found.Id.Value.IndexOf('@', StringComparison.Ordinal)]);
        return new GetWorkflowRunResponse(found.Id, slug, found.Revision, RunText.Of(found.Status), RunText.Json(found.Trigger), RunText.Json(found.Outputs), found.Error, found.ItemId, found.StartedAt, found.EndedAt,
            [.. nodes.Value.Select(n => new NodeRunView(n.NodeId, n.ItemIndex, RunText.Of(n.Status), RunText.Json(n.Input), RunText.Json(n.Output), n.Truncated, n.Error, agentNodes.Of(n), n.Raw))]);
    }

    // The agent state of the run's rows. The candidates are the 'run_agent' nodes of the revision the run walked whose row runs now (item 0: a run_agent node runs once). The run's workflow says which
    // one it waits on and whether it is held; when it can't be asked (Temporal down, no workflow), the held marker of the node's launch files answers instead. A revision the store can't give leaves no
    // candidate: the read still answers, with no agent field.
    private async ValueTask<AgentRows> AgentNodes(WorkflowRun run, IReadOnlyList<NodeRun> rows, CancellationToken cancellationToken)
    {
        if (run.Status is not (WorkflowRunStatus.Running or WorkflowRunStatus.Waiting) || !rows.Any(static r => r.Status == NodeRunStatus.Running && r.ItemIndex == 0))
        {
            return AgentRows.None(run.Id);
        }

        var revision = await workflows.GetRevision(run.WorkflowId, run.Revision, cancellationToken).ConfigureAwait(false);
        if (revision.IsError)
        {
            return AgentRows.None(run.Id);
        }

        var agents = revision.Value.Spec.Nodes.Where(static n => string.Equals(n.Type, "run_agent", StringComparison.Ordinal)).Select(static n => n.Id).ToHashSet();
        var asked = await control.Running(run.Id, cancellationToken).ConfigureAwait(false);
        return new AgentRows(run.Id, agents, files, Asked: !asked.IsError, asked.IsError ? null : asked.Value);
    }
}
