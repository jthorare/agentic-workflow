using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;

namespace Aw.Sindri.Features.PauseWorkflow;

/// <summary>Clears a workflow's active revision, so no trigger starts it; every revision stays saved. Pausing a workflow that is already off still succeeds. An unknown slug is SND-GRAPH-008 and an archived workflow SND-GRAPH-011.</summary>
/// <param name="workflows">The workflows.</param>
/// <param name="log">Writes the lifecycle event.</param>
/// <param name="callers">The current caller.</param>
internal sealed class PauseWorkflowService(IWorkflowStore workflows, WorkflowChangeLog log, ICallerContextAccessor callers) : IService<PauseWorkflowRequest, PauseWorkflowResponse>
{
    public async ValueTask<Result<PauseWorkflowResponse>> Handle(PauseWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current?.Class is not (CallerClass.Human or CallerClass.Service))
        {
            return SindriErrors.AuthRefused.With("Turning a workflow off is the owner's to do; a session can't (fix: ask the owner to press Pause)");
        }

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
            return SindriErrors.GraphArchived.With($"'{request.Slug.Value}' is archived; there is nothing to pause");
        }

        if (log.LeaseProblem() is { } down)
        {
            return down;
        }

        var paused = await workflows.Pause(record.Id, cancellationToken).ConfigureAwait(false);
        if (paused.IsError)
        {
            return paused.Error;
        }

        var recorded = await log.Record(EventTypes.WorkflowPaused, new WorkflowChangedPayload(record.Id, record.Slug, paused.Value.LatestRevision, SpecHash: null, request.By), cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new PauseWorkflowResponse(record.Slug, paused.Value.LatestRevision);
    }
}
