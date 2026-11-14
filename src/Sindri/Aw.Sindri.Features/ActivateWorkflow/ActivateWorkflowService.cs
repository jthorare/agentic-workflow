using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Features.ActivateWorkflow;

/// <summary>
/// Activates a revision after it passes the activation checks (findings are SND-GRAPH findings and nothing changes). An unknown slug or revision is SND-GRAPH-008, an archived workflow SND-GRAPH-011.
/// The revision's declared resources are provisioned first (agent-first PR 14): a failure (SND-RES-001 and the rest) refuses the activate and the workflow stays as it was.
/// </summary>
/// <param name="workflows">The workflows.</param>
/// <param name="checker">Builds the validator.</param>
/// <param name="log">Writes the lifecycle event.</param>
/// <param name="callers">The current caller.</param>
/// <param name="provisioner">Makes the revision's declared resources exist (agent-first PR 14); null on a build with no resources.</param>
internal sealed class ActivateWorkflowService(IWorkflowStore workflows, IWorkflowChecker checker, WorkflowChangeLog log, ICallerContextAccessor callers, IResourceProvisioner? provisioner = null) : IService<ActivateWorkflowRequest, ActivateWorkflowResponse>
{
    public async ValueTask<Result<ActivateWorkflowResponse>> Handle(ActivateWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current?.Class is not (CallerClass.Human or CallerClass.Service))
        {
            return SindriErrors.AuthRefused.With("Turning a workflow on is the owner's to do; a session can't (fix: ask the owner to press Activate)");
        }

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
            ? SindriErrors.GraphArchived.With($"'{slug.Value}' is archived; create a new workflow from its spec under another slug (an archived slug stays reserved) to run it again")
            : record;
    }

    private async ValueTask<Result<ActivateWorkflowResponse>> ActivateAndRecord(WorkflowRecord record, WorkflowRevision revision, string by, CancellationToken cancellationToken)
    {
        if (log.LeaseProblem() is { } down)
        {
            return down;
        }

        if (await Provision(record.Slug, revision.Spec, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        var activated = await workflows.Activate(record.Id, revision.Revision, cancellationToken).ConfigureAwait(false);
        if (activated.IsError)
        {
            return activated.Error;
        }

        var recorded = await log.Record(EventTypes.WorkflowActivated, new WorkflowChangedPayload(record.Id, record.Slug, revision.Revision, revision.SpecHash, by), cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new ActivateWorkflowResponse(record.Slug, revision.Revision, revision.SpecHash);
    }

    // why: the same files a run gets (GraphResourceEnsurer before each node), made here so a bad declaration or a full disk shows at activate, not at the first run
    private async ValueTask<Error?> Provision(WorkflowSlug slug, GraphSpec spec, CancellationToken cancellationToken)
    {
        if (provisioner is null || spec.Resources is null)
        {
            return null;
        }

        var owner = ResourceOwner.Parse($"workflow:{slug.Value}");
        if (owner.IsError)
        {
            return owner.Error;
        }

        var ensured = await provisioner.Ensure(owner.Value, GraphResourceSpec.From(spec.Resources), cancellationToken).ConfigureAwait(false);
        return ensured.IsError ? ensured.Error : null;
    }
}
