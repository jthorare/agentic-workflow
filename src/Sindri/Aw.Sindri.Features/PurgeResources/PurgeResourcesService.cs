using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Features.PurgeResources;

/// <summary>
/// Deletes an owner's resources. A malformed owner is SND-RES-006. A purge is refused (SND-RES-003) while a run of the owner's workflow is queued, running or waiting (the message names the run)
/// and while the workflow is active. An archived or off workflow, a workflow that no longer exists and a toolkit owner (no toolkit runs yet) purge. Archiving never calls this.
/// </summary>
/// <param name="files">The resources on disk.</param>
/// <param name="workflows">The workflows.</param>
/// <param name="runs">The runs.</param>
internal sealed class PurgeResourcesService(IResourceFiles files, IWorkflowStore workflows, IWorkflowRunStore runs) : IService<PurgeResourcesRequest, PurgeResourcesResponse>
{
    public async ValueTask<Result<PurgeResourcesResponse>> Handle(PurgeResourcesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var owner = ResourceOwner.Parse(request.Owner);
        if (owner.IsError)
        {
            return owner.Error;
        }

        var refused = await Refusal(owner.Value, cancellationToken).ConfigureAwait(false);
        if (refused is not null)
        {
            return refused;
        }

        // why: a run can start after the first check; the rename takes the folder away from it, the second check then decides
        var staged = files.Stage(owner.Value);
        if (staged.IsError)
        {
            return staged.Error;
        }

        var again = await Refusal(owner.Value, cancellationToken).ConfigureAwait(false);
        if (again is not null)
        {
            var restored = staged.Value.Restore();
            return restored.IsError ? restored.Error : again;
        }

        var purged = staged.Value.Delete();
        return purged.IsError ? purged.Error : new PurgeResourcesResponse(owner.Value.ToString(), purged.Value);
    }

    /// <summary>Why the owner can't be purged now, or null. A node's resources belong to its workflow, so the workflow's state decides.</summary>
    private async ValueTask<Error?> Refusal(ResourceOwner owner, CancellationToken cancellationToken)
    {
        if (owner.Kind == ResourceOwnerKind.Toolkit)
        {
            return null;
        }

        var slug = owner.Key.Split('/')[0];
        var listed = await workflows.List(includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var record = listed.Value.FirstOrDefault(r => string.Equals(r.Slug.Value, slug, StringComparison.Ordinal));
        if (record is null)
        {
            return null;
        }

        var inFlight = await runs.InFlight(record.Id, cancellationToken).ConfigureAwait(false);
        if (inFlight.IsError)
        {
            return inFlight.Error;
        }

        if (inFlight.Value.Count > 0)
        {
            return SindriErrors.ResourcePurgeRefused.With($"the run '{inFlight.Value[0].Id.Value}' of workflow '{slug}' is still in flight; let it end or cancel it, then purge {owner}");
        }

        return record.ArchivedAt is null && record.ActiveRevision is not null
            ? SindriErrors.ResourcePurgeRefused.With($"workflow '{slug}' is active; turn it off or archive it, then purge {owner}")
            : null;
    }
}
