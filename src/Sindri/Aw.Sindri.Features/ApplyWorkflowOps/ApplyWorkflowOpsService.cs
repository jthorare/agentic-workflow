using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Ops;
using Aw.Sindri.Core.Graph.Validation;

namespace Aw.Sindri.Features.ApplyWorkflowOps;

/// <summary>
/// Applies a batch of ops to the newest revision and saves the result as the next one. Nothing is written when the base revision is stale (SND-GRAPH-007 with the current revision), when any op fails
/// (SND-GRAPH-012, none of the batch applies) or when the edited graph fails the draft checks. An unknown slug is SND-GRAPH-008, an archived workflow SND-GRAPH-011.
/// The draft check (it compiles every code node) runs before the command gate; inside the gate the workflow is read again, and a revision that moved meanwhile is SND-GRAPH-007 (#3571).
/// </summary>
/// <param name="workflows">The workflows.</param>
/// <param name="checker">Builds the validator.</param>
/// <param name="log">Writes the lifecycle event.</param>
/// <param name="gate">The command gate, taken around the write only.</param>
internal sealed class ApplyWorkflowOpsService(IWorkflowStore workflows, IWorkflowChecker checker, WorkflowChangeLog log, ICommandGate gate) : IService<ApplyWorkflowOpsRequest, ApplyWorkflowOpsResponse>
{
    public async ValueTask<Result<ApplyWorkflowOpsResponse>> Handle(ApplyWorkflowOpsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ops = GraphOpJson.Parse(request.Ops);
        if (ops.IsError)
        {
            return ops.Error;
        }

        var found = await FindActive(request.Slug, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var record = found.Value;
        var current = await workflows.GetRevision(record.Id, record.LatestRevision, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        var validator = await checker.Validator(cancellationToken).ConfigureAwait(false);
        var edited = GraphOps.Apply(current.Value.Spec, request.BaseRevision, record.LatestRevision, ops.Value, validator);
        if (edited.IsError)
        {
            return edited.Error;
        }

        return await SaveInGate(request, record, edited.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<ApplyWorkflowOpsResponse>> SaveInGate(ApplyWorkflowOpsRequest request, WorkflowRecord record, GraphSpec edited, CancellationToken cancellationToken)
    {
        using var hold = await gate.Enter(cancellationToken).ConfigureAwait(false);
        if (hold is null)
        {
            return WorkflowWriteErrors.Busy;
        }

        var again = await FindActive(request.Slug, cancellationToken).ConfigureAwait(false);   // why: a write may have landed between the check above and the gate
        if (again.IsError)
        {
            return again.Error;
        }

        return again.Value.LatestRevision != record.LatestRevision
            ? WorkflowWriteErrors.Stale(again.Value.LatestRevision, request.BaseRevision, "these ops")
            : await SaveAndRecord(again.Value, edited, request.By, cancellationToken).ConfigureAwait(false);
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
            ? SindriErrors.GraphArchived.With($"'{slug.Value}' is archived; create a new workflow from its spec under another slug (an archived slug stays reserved) to edit it")
            : record;
    }

    private async ValueTask<Result<ApplyWorkflowOpsResponse>> SaveAndRecord(WorkflowRecord record, GraphSpec edited, string by, CancellationToken cancellationToken)
    {
        if (log.LeaseProblem() is { } down)
        {
            return down;
        }

        var saved = await workflows.Save(record.Id, record.LatestRevision, edited, by, cancellationToken).ConfigureAwait(false);
        if (saved.IsError)
        {
            return saved.Error;
        }

        var recorded = await log.Record(EventTypes.WorkflowRevised, new WorkflowChangedPayload(record.Id, record.Slug, saved.Value.Revision, saved.Value.SpecHash, by), cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new ApplyWorkflowOpsResponse(record.Slug, saved.Value.Revision, saved.Value.SpecHash);
    }
}
