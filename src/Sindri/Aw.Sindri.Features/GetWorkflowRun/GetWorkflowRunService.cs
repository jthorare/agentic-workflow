using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.GetWorkflowRun;

/// <summary>One run with its node runs; an unknown run is SND-GRAPH-101. A node payload the store cut short comes back as text, with the node's Truncated flag set.</summary>
/// <param name="runs">The runs and node runs.</param>
internal sealed class GetWorkflowRunService(IWorkflowRunStore runs) : IService<GetWorkflowRunRequest, GetWorkflowRunResponse>
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
        var slug = WorkflowSlug.From(found.Id.Value[..found.Id.Value.IndexOf('@', StringComparison.Ordinal)]);
        return new GetWorkflowRunResponse(found.Id, slug, found.Revision, RunText.Of(found.Status), RunText.Json(found.Trigger), RunText.Json(found.Outputs), found.Error, found.ItemId, found.StartedAt, found.EndedAt,
            [.. nodes.Value.Select(static n => new NodeRunView(n.NodeId, n.ItemIndex, RunText.Of(n.Status), RunText.Json(n.Input), RunText.Json(n.Output), n.Truncated, n.Error))]);
    }
}
