using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.ListWorkflowRuns;

/// <summary>A page of a workflow's runs, newest first, whatever their status; an unknown slug is SND-GRAPH-008 (an archived workflow keeps its runs readable).</summary>
/// <param name="workflows">The workflows.</param>
/// <param name="runs">The runs.</param>
internal sealed class ListWorkflowRunsService(IWorkflowStore workflows, IWorkflowRunStore runs) : IService<ListWorkflowRunsRequest, ListWorkflowRunsResponse>
{
    /// <summary>How many runs a page holds when the call names no limit, and the most it holds when the limit is larger.</summary>
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

        var limit = Math.Clamp(request.Limit, 1, Newest);
        var recent = await runs.Recent(record.Id, Math.Max(request.Offset, 0), limit + 1, cancellationToken).ConfigureAwait(false);
        if (recent.IsError)
        {
            return recent.Error;
        }

        return new ListWorkflowRunsResponse(record.Slug, [.. recent.Value.Take(limit).Select(static r => new WorkflowRunSummary(r.Id, r.Revision, RunWords.Of(r.Status), RunWords.TriggerWord(r.Trigger), r.StartedAt, r.EndedAt, r.Error) { Seconds = RunWords.Seconds(r.StartedAt, r.EndedAt), Result = RunWords.Result(r) })], More: recent.Value.Count > limit);
    }
}
