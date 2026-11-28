using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Features.DeleteWorkflow;

/// <summary>
/// Deletes a workflow (Workflows page spec §4.9): it is archived (the store never deletes revisions or runs), then the resources of <c lang="text">workflow:slug</c> and every
/// <c lang="text">node:slug/id</c> are purged the way <c lang="csharp">PurgeResourcesService</c> purges them (stage, check the runs again, delete). An unknown slug is SND-GRAPH-008, an archived
/// workflow SND-GRAPH-011, an active one SND-GRAPH-114 and one with a run in flight, live or test, SND-GRAPH-115 (the message names the oldest run). A purge that fails does not
/// undo the archive: the answer names the owners whose resources stayed.
/// </summary>
/// <param name="workflows">The workflows.</param>
/// <param name="runs">The runs: a run in flight blocks the delete, and the answer says how many runs stay.</param>
/// <param name="files">The resources on disk.</param>
/// <param name="log">Writes the lifecycle event.</param>
internal sealed class DeleteWorkflowService(IWorkflowStore workflows, IWorkflowRunStore runs, IResourceFiles files, WorkflowChangeLog log) : IService<DeleteWorkflowRequest, DeleteWorkflowResponse>
{
    public async ValueTask<Result<DeleteWorkflowResponse>> Handle(DeleteWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var record = await Find(request.Slug, cancellationToken).ConfigureAwait(false);
        if (record.IsError)
        {
            return record.Error;
        }

        if (await Refusal(record.Value, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        var at = await Archive(record.Value, request.By, cancellationToken).ConfigureAwait(false);
        if (at.IsError)
        {
            return at.Error;
        }

        var kept = await runs.Count(record.Value.Id, cancellationToken).ConfigureAwait(false);
        if (kept.IsError)
        {
            return kept.Error;
        }

        // why: the archive stands even when a purge fails; the answer names the owners whose resources stayed instead of failing the delete
        var (freed, stayed) = await Purge(record.Value, cancellationToken).ConfigureAwait(false);
        return new DeleteWorkflowResponse(record.Value.Slug, at.Value, kept.Value, freed, stayed);
    }

    /// <summary>The workflow by slug: SND-GRAPH-008 when there is none, SND-GRAPH-011 when it is archived.</summary>
    private async ValueTask<Result<WorkflowRecord>> Find(WorkflowSlug slug, CancellationToken cancellationToken)
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

        return record.ArchivedAt is null ? record : SindriErrors.GraphArchived.With($"'{slug.Value}' is already archived");
    }

    /// <summary>Why the workflow can't be deleted now, or null: SND-GRAPH-114 when it is active, SND-GRAPH-115 naming the oldest run in flight.</summary>
    private async ValueTask<Error?> Refusal(WorkflowRecord record, CancellationToken cancellationToken)
    {
        if (record.ActiveRevision is not null)
        {
            return SindriErrors.GraphDeleteActive.With($"'{record.Slug.Value}' is active; pause it first, then delete it");
        }

        var inFlight = await runs.InFlight(record.Id, cancellationToken).ConfigureAwait(false);
        if (inFlight.IsError)
        {
            return inFlight.Error;
        }

        return inFlight.Value.Count > 0
            ? SindriErrors.GraphDeleteRunInFlight.With($"the run '{inFlight.Value[0].Id.Value}' of '{record.Slug.Value}' is still going; delete the workflow after it ends or cancel the run")
            : null;
    }

    /// <summary>Archives the workflow and writes the lifecycle event. Returns when it was archived.</summary>
    private async ValueTask<Result<DateTimeOffset>> Archive(WorkflowRecord record, string by, CancellationToken cancellationToken)
    {
        if (log.LeaseProblem() is { } down)
        {
            return down;
        }

        var archived = await workflows.Archive(record.Id, cancellationToken).ConfigureAwait(false);
        if (archived.IsError)
        {
            return archived.Error;
        }

        var recorded = await log.Record(EventTypes.WorkflowArchived, new WorkflowChangedPayload(record.Id, record.Slug, archived.Value.LatestRevision, SpecHash: null, by), cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            return recorded.Error;
        }

        return archived.Value.ArchivedAt is { } at ? at : SindriErrors.GraphNotFound.With("the workflow was not archived");
    }

    /// <summary>Purges <c lang="text">workflow:slug</c> and every <c lang="text">node:slug/id</c> on disk. Returns the bytes freed and the owners that stayed.</summary>
    private async ValueTask<(long Freed, IReadOnlyList<string> Stayed)> Purge(WorkflowRecord record, CancellationToken cancellationToken)
    {
        var slug = record.Slug.Value;
        var listed = files.List(owner: null);
        if (listed.IsError)
        {
            return (0L, [$"workflow:{slug}"]);
        }

        var mine = listed.Value.Where(e => Belongs(e.Owner, slug)).ToList();
        var owners = mine.Select(static e => e.Owner).Distinct().ToList();
        if (ResourceOwner.Parse($"workflow:{slug}") is { IsSuccess: true } workflowOwner && !owners.Contains(workflowOwner.Value))
        {
            owners.Insert(0, workflowOwner.Value);   // why: a folder that holds no database or disk (stray files) is not listed, but is still the workflow's
        }

        var freed = 0L;
        var stayed = new List<string>();
        foreach (var owner in owners)
        {
            if (await TryPurge(record, owner, cancellationToken).ConfigureAwait(false))
            {
                freed += mine.Where(e => e.Owner == owner).Sum(static e => e.SizeBytes);
            }
            else
            {
                stayed.Add(owner.ToString());
            }
        }

        return (freed, stayed);
    }

    /// <summary>Stage, check the runs again (a run may have started after the first check), then delete or put back; the same steps as <c lang="csharp">PurgeResourcesService</c>.</summary>
    private async ValueTask<bool> TryPurge(WorkflowRecord record, ResourceOwner owner, CancellationToken cancellationToken)
    {
        var staged = files.Stage(owner);
        if (staged.IsError)
        {
            return false;
        }

        var again = await runs.InFlight(record.Id, cancellationToken).ConfigureAwait(false);
        if (again.IsError || again.Value.Count > 0)
        {
            _ = staged.Value.Restore();
            return false;
        }

        return staged.Value.Delete().IsSuccess;
    }

    private static bool Belongs(ResourceOwner owner, string slug) => owner.Kind switch
    {
        ResourceOwnerKind.Workflow => string.Equals(owner.Key, slug, StringComparison.Ordinal),
        ResourceOwnerKind.Node => owner.Key.StartsWith(slug + "/", StringComparison.Ordinal),
        ResourceOwnerKind.Toolkit => false,
        _ => false,
    };
}
