using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Graph;

namespace Aw.Sindri.Features.ListWorkflows;

/// <summary>The workflows that are not archived, by slug, each with who saved its newest revision.</summary>
/// <param name="workflows">The workflows and their revisions.</param>
internal sealed class ListWorkflowsService(IWorkflowStore workflows) : IService<ListWorkflowsRequest, ListWorkflowsResponse>
{
    public async ValueTask<Result<ListWorkflowsResponse>> Handle(ListWorkflowsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var listed = await workflows.List(includeArchived: false, cancellationToken).ConfigureAwait(false);
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

            rows.Add(new WorkflowSummary(record.Slug, record.Name, record.ActiveRevision, record.LatestRevision, newest.Value.CreatedBy, newest.Value.CreatedAt));
        }

        return new ListWorkflowsResponse(rows);
    }
}
