using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;

namespace Aw.Sindri.Features.ReplaceWorkflow;

/// <summary>
/// Saves a whole new graph as the next revision. A base revision that is not the latest is SND-GRAPH-007 with the current revision and nothing is written; a graph that fails the draft checks is
/// findings (SND-GRAPH) and nothing is written. An unknown slug is SND-GRAPH-008, an archived workflow SND-GRAPH-011.
/// The draft check (it compiles every code node) runs before the command gate; inside the gate the workflow is read again, and a revision that moved meanwhile is SND-GRAPH-007 (#3571).
/// </summary>
/// <param name="workflows">The workflows.</param>
/// <param name="checker">Builds the validator.</param>
/// <param name="log">Writes the lifecycle event.</param>
/// <param name="gate">The command gate, taken around the write only.</param>
internal sealed class ReplaceWorkflowService(IWorkflowStore workflows, IWorkflowChecker checker, WorkflowChangeLog log, ICommandGate gate) : IService<ReplaceWorkflowRequest, ReplaceWorkflowResponse>
{
    public async ValueTask<Result<ReplaceWorkflowResponse>> Handle(ReplaceWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var spec = GraphJson.Parse(request.Spec);
        if (spec.IsError)
        {
            return spec.Error;
        }

        var found = await FindActive(request.Slug, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var record = found.Value;
        if (request.BaseRevision != record.LatestRevision)
        {
            return StaleRevision(record.LatestRevision, request.BaseRevision);
        }

        var validator = await checker.Validator(cancellationToken).ConfigureAwait(false);
        var validation = validator.Validate(spec.Value, GraphMode.Draft);
        if (!validation.Passed)
        {
            return SindriErrors.GraphFindings.With(string.Join("; ", validation.Blocking.Select(static f => f.ToString())));
        }

        return await SaveInGate(spec.Value, request, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<ReplaceWorkflowResponse>> SaveInGate(GraphSpec spec, ReplaceWorkflowRequest request, CancellationToken cancellationToken)
    {
        using var hold = await gate.Enter(cancellationToken).ConfigureAwait(false);
        if (hold is null)
        {
            return WorkflowWriteErrors.Busy;
        }

        var again = await FindActive(request.Slug, cancellationToken).ConfigureAwait(false);   // why: a write may have landed between the draft check and the gate
        if (again.IsError)
        {
            return again.Error;
        }

        return request.BaseRevision != again.Value.LatestRevision
            ? StaleRevision(again.Value.LatestRevision, request.BaseRevision)
            : await SaveAndRecord(again.Value, spec, request, cancellationToken).ConfigureAwait(false);
    }

    private static Error StaleRevision(int latest, int baseRevision) => WorkflowWriteErrors.Stale(latest, baseRevision, "this replace");

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

    private async ValueTask<Result<ReplaceWorkflowResponse>> SaveAndRecord(WorkflowRecord record, GraphSpec spec, ReplaceWorkflowRequest request, CancellationToken cancellationToken)
    {
        if (log.LeaseProblem() is { } down)
        {
            return down;
        }

        var saved = await workflows.Save(record.Id, request.BaseRevision, spec, request.By, cancellationToken).ConfigureAwait(false);
        if (saved.IsError)
        {
            return saved.Error;
        }

        var recorded = await log.Record(EventTypes.WorkflowRevised, new WorkflowChangedPayload(record.Id, record.Slug, saved.Value.Revision, saved.Value.SpecHash, request.By), cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new ReplaceWorkflowResponse(record.Slug, saved.Value.Revision, saved.Value.SpecHash);
    }
}
