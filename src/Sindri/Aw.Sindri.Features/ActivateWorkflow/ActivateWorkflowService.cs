using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;

namespace Aw.Sindri.Features.ActivateWorkflow;

/// <summary>
/// Activates a revision after it passes the activation checks (findings are SND-GRAPH findings and nothing changes). An unknown slug or revision is SND-GRAPH-008, an archived workflow SND-GRAPH-011.
/// </summary>
/// <param name="workflows">The workflows.</param>
/// <param name="checker">Builds the validator.</param>
/// <param name="log">Writes the lifecycle event.</param>
internal sealed class ActivateWorkflowService(IWorkflowStore workflows, IWorkflowChecker checker, WorkflowChangeLog log) : IService<ActivateWorkflowRequest, ActivateWorkflowResponse>
{
    public async ValueTask<Result<ActivateWorkflowResponse>> Handle(ActivateWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var found = await FindActive(request.Slug, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var record = found.Value;
        var revision = await workflows.GetRevision(record.Id, request.Revision ?? record.LatestRevision, cancellationToken).ConfigureAwait(false);
        if (revision.IsError)
        {
            return revision.Error;
        }

        var validator = await checker.Validator(cancellationToken).ConfigureAwait(false);
        var result = validator.Validate(revision.Value.Spec, GraphMode.Activate);
        if (!result.Passed)
        {
            return SindriErrors.GraphFindings.With(string.Join("; ", result.Blocking.Select(static f => f.ToString())));
        }

        return await ActivateAndRecord(record, revision.Value, request.By, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<WorkflowRecord>> FindActive(WorkflowSlug slug, CancellationToken cancellationToken)
    {
        var listed = await workflows.List(includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var record = listed.Value.FirstOrDefault(r => r.Slug == slug);
        if (record is null)
        {
            return SindriErrors.GraphNotFound.With($"no workflow '{slug.Value}'; list the workflows to see what exists");
        }

        return record.ArchivedAt is not null
            ? SindriErrors.GraphArchived.With($"'{slug.Value}' is archived; create a new workflow from its spec to run it again")
            : record;
    }

    private async ValueTask<Result<ActivateWorkflowResponse>> ActivateAndRecord(WorkflowRecord record, WorkflowRevision revision, string by, CancellationToken cancellationToken)
    {
        if (log.LeaseProblem() is { } down)
        {
            return down;
        }

        var activated = await workflows.Activate(record.Id, revision.Revision, cancellationToken).ConfigureAwait(false);
        if (activated.IsError)
        {
            return activated.Error;
        }

        var recorded = await log.Record(EventTypes.WorkflowActivated, new WorkflowChangedPayload(record.Id, record.Slug, revision.Revision, revision.SpecHash, by), cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new ActivateWorkflowResponse(record.Slug, revision.Revision, revision.SpecHash);
    }
}
