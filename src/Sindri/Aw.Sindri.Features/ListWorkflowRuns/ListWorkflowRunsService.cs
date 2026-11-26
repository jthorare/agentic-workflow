using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.ListWorkflowRuns;

/// <summary>
/// A page of a workflow's runs, newest first, kept by the request's filter (#4334) in the store's query; an unknown slug is SND-GRAPH-008 (an archived workflow keeps its runs readable),
/// an unknown 'before' run SND-GRAPH-101 and a 'before' run of another workflow SND-GRAPH-134.
/// </summary>
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

        var query = await Query(record, request.Filter, cancellationToken).ConfigureAwait(false);
        if (query.IsError)
        {
            return query.Error;
        }

        var limit = Math.Clamp(request.Limit, 1, Newest);
        var found = await runs.Find(record.Id, query.Value, Math.Max(request.Offset, 0), limit + 1, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        List<WorkflowRunSummary> page = [.. found.Value.Take(limit).Select(static r => new WorkflowRunSummary(r.Id, r.Revision, RunWords.Of(r.Status), RunWords.TriggerWord(r.Trigger), r.StartedAt, r.EndedAt, r.Error) { Seconds = RunWords.Seconds(r.StartedAt, r.EndedAt), Result = RunWords.Result(r) })];
        var more = found.Value.Count > limit;
        return new ListWorkflowRunsResponse(record.Slug, page, more, Next: more ? page[^1].Id : null);
    }

    // why: the cursor is a run id; the store pages from that run's start time and occurrence, so the run is read once here, and one of another workflow is refused rather than paging from it
    private async ValueTask<Result<WorkflowRunQuery>> Query(WorkflowRecord record, RunsFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Before is not { } cursor)
        {
            return new WorkflowRunQuery(filter.Statuses, filter.Since, filter.Until, Before: null);
        }

        var before = await runs.Get(cursor, cancellationToken).ConfigureAwait(false);
        if (before.IsError)
        {
            return before.Error;
        }

        return before.Value.WorkflowId == record.Id
            ? new WorkflowRunQuery(filter.Statuses, filter.Since, filter.Until, before.Value)
            : SindriErrors.GraphRunsFilterInvalid.With($"'before': '{cursor.Value}' is not a run of {record.Slug.Value}; send the 'next' of this workflow's page");
    }
}
