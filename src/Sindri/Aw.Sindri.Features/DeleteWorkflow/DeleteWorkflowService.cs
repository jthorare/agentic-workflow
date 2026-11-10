using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;

namespace Aw.Sindri.Features.DeleteWorkflow;

/// <summary>Archives a workflow (the store never deletes revisions or runs). An unknown slug is SND-GRAPH-008; an archived workflow is SND-GRAPH-011, since there is nothing left to archive.</summary>
/// <param name="workflows">The workflows.</param>
/// <param name="log">Writes the lifecycle event.</param>
internal sealed class DeleteWorkflowService(IWorkflowStore workflows, WorkflowChangeLog log) : IService<DeleteWorkflowRequest, DeleteWorkflowResponse>
{
    public async ValueTask<Result<DeleteWorkflowResponse>> Handle(DeleteWorkflowRequest request, CancellationToken cancellationToken)
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

        if (record.ArchivedAt is not null)
        {
            return SindriErrors.GraphArchived.With($"'{request.Slug.Value}' is already archived");
        }

        if (log.LeaseProblem() is { } down)
        {
            return down;
        }

        var archived = await workflows.Archive(record.Id, cancellationToken).ConfigureAwait(false);
        if (archived.IsError)
        {
            return archived.Error;
        }

        var recorded = await log.Record(EventTypes.WorkflowArchived, new WorkflowChangedPayload(record.Id, record.Slug, archived.Value.LatestRevision, SpecHash: null, request.By), cancellationToken).ConfigureAwait(false);
        return recorded.IsError || archived.Value.ArchivedAt is not { } at ? (recorded.IsError ? recorded.Error : SindriErrors.GraphNotFound.With("the workflow was not archived")) : new DeleteWorkflowResponse(record.Slug, at);
    }
}
