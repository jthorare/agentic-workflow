using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.GetWorkflowRun;

/// <summary>One run with its node runs; an unknown run is SND-GRAPH-101. A node payload the store cut short comes back as text, with the node's Truncated flag set.</summary>
/// <param name="runs">The runs and node runs.</param>
/// <param name="workflows">The revision the run walked: it says which nodes are 'run_agent' nodes.</param>
/// <param name="files">The per-launch files: the held marker says whether a person holds an agent node.</param>
internal sealed class GetWorkflowRunService(IWorkflowRunStore runs, IWorkflowStore workflows, ILaunchFiles files) : IService<GetWorkflowRunRequest, GetWorkflowRunResponse>
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
        var agentNodes = await AgentNodes(found, cancellationToken).ConfigureAwait(false);
        var slug = WorkflowSlug.From(found.Id.Value[..found.Id.Value.IndexOf('@', StringComparison.Ordinal)]);
        return new GetWorkflowRunResponse(found.Id, slug, found.Revision, RunText.Of(found.Status), RunText.Json(found.Trigger), RunText.Json(found.Outputs), found.Error, found.ItemId, found.StartedAt, found.EndedAt,
            [.. nodes.Value.Select(n => new NodeRunView(n.NodeId, n.ItemIndex, RunText.Of(n.Status), RunText.Json(n.Input), RunText.Json(n.Output), n.Truncated, n.Error, AgentOf(found.Id, n, agentNodes)))]);
    }

    // The ids of the run's 'run_agent' nodes, from the revision it walked. A revision the store can't give (a run of a deleted workflow) leaves the set empty: the read still answers, with no agent field.
    private async ValueTask<HashSet<NodeId>> AgentNodes(WorkflowRun run, CancellationToken cancellationToken)
    {
        if (run.Status is not (WorkflowRunStatus.Running or WorkflowRunStatus.Waiting))
        {
            return [];
        }

        var revision = await workflows.GetRevision(run.WorkflowId, run.Revision, cancellationToken).ConfigureAwait(false);
        return revision.IsError ? [] : [.. revision.Value.Spec.Nodes.Where(static n => string.Equals(n.Type, "run_agent", StringComparison.Ordinal)).Select(static n => n.Id)];
    }

    // 'waiting' while a run_agent node runs, 'taken_over' while a person holds it (the held marker the workflow keeps in the node's launch files); a run_agent node runs once, so only item 0 has a launch. Null for any other row.
    private string? AgentOf(WorkflowRunId run, NodeRun row, HashSet<NodeId> agentNodes)
    {
        if (row.Status != NodeRunStatus.Running || row.ItemIndex != 0 || !agentNodes.Contains(row.NodeId))
        {
            return null;
        }

        return files.TakenOver(NodeRunAddress.Of(run, row.NodeId).Launch()) ? "taken_over" : "waiting";
    }
}
