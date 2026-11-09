using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.ListWorkflowRuns;

/// <summary>A workflow's newest runs, newest first, whatever their status; an unknown slug is SND-GRAPH-008 (an archived workflow keeps its runs readable).</summary>
/// <param name="workflows">The workflows.</param>
/// <param name="runs">The runs.</param>
internal sealed class ListWorkflowRunsService(IWorkflowStore workflows, IWorkflowRunStore runs) : IService<ListWorkflowRunsRequest, ListWorkflowRunsResponse>
{
    /// <summary>How many runs the list keeps.</summary>
    internal const int Newest = 50;

    public async ValueTask<Result<ListWorkflowRunsResponse>> Handle(ListWorkflowRunsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var listed = await workflows.List(includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var record = listed.Value.FirstOrDefault(r => r.Slug == request.Slug);
        if (record is null)
        {
            return SindriErrors.GraphNotFound.With($"no workflow '{request.Slug.Value}'; list the workflows to see what exists");
        }

        var recent = await runs.Recent(record.Id, Newest, cancellationToken).ConfigureAwait(false);
        if (recent.IsError)
        {
            return recent.Error;
        }

        return new ListWorkflowRunsResponse(record.Slug, [.. recent.Value.Select(static r => new WorkflowRunSummary(r.Id, r.Revision, RunWords.Of(r.Status), RunWords.Kind(r.Trigger), r.StartedAt, r.EndedAt, r.Error))]);
    }
}
