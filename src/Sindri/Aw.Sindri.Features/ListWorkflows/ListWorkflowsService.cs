using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.ListWorkflows;

/// <summary>The workflows that are not archived (or, asked, all of them with the archived ones marked), by slug, each with who saved its newest revision.</summary>
/// <param name="workflows">The workflows and their revisions.</param>
/// <param name="runs">The runs behind the last-run chip and the strip (issue 3683).</param>
/// <param name="clock">The time the next run is measured from (issue 3683).</param>
internal sealed class ListWorkflowsService(IWorkflowStore workflows, IWorkflowRunStore runs, TimeProvider clock) : IService<ListWorkflowsRequest, ListWorkflowsResponse>
{
    public async ValueTask<Result<ListWorkflowsResponse>> Handle(ListWorkflowsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = (runs, clock);   // red stub (issue 3683): the green reads last runs, the trigger words and the next run
        var listed = await workflows.List(request.IncludeArchived, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var rows = new List<WorkflowSummary>(listed.Value.Count);
        foreach (var record in listed.Value)
        {
            var newest = await workflows.GetRevision(record.Id, record.LatestRevision, cancellationToken).ConfigureAwait(false);
            if (newest.IsError)
            {
                return newest.Error;
            }

            rows.Add(new WorkflowSummary(record.Slug, record.Name, record.ActiveRevision, record.LatestRevision, newest.Value.CreatedBy, newest.Value.CreatedAt) { Archived = record.ArchivedAt is null ? null : true });
        }

        return new ListWorkflowsResponse(rows);
    }
}
