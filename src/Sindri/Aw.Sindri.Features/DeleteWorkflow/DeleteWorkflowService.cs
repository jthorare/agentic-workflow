using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Features.DeleteWorkflow;

/// <summary>Archives a workflow (the store never deletes revisions or runs). An unknown slug is SND-GRAPH-008; an archived workflow is SND-GRAPH-011, since there is nothing left to archive.</summary>
/// <param name="workflows">The workflows.</param>
/// <param name="runs">The runs; a run in flight blocks the delete.</param>
/// <param name="files">The resources on disk, purged after the archive.</param>
/// <param name="log">Writes the lifecycle event.</param>
internal sealed class DeleteWorkflowService(IWorkflowStore workflows, IWorkflowRunStore runs, IResourceFiles files, WorkflowChangeLog log) : IService<DeleteWorkflowRequest, DeleteWorkflowResponse>
{
    public async ValueTask<Result<DeleteWorkflowResponse>> Handle(DeleteWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var found = await Find(request.Slug, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var record = found.Value;
        var kept = await RunsKept(record, cancellationToken).ConfigureAwait(false);
        if (kept.IsError)
        {
            return kept.Error;
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
        if (recorded.IsError)
        {
            return recorded.Error;
        }

        if (archived.Value.ArchivedAt is not { } at)
        {
            return SindriErrors.GraphNotFound.With("the workflow was not archived");
        }

        // why: the archive stands even when a purge fails; the response names the owners whose folders stayed, and 'sindri doctor' lists them as orphans.
        var (freed, stayed) = Purge(record.Slug.Value);
        return new DeleteWorkflowResponse(record.Slug, at, kept.Value, freed, stayed);
    }

    private async ValueTask<Result<WorkflowRecord>> Find(WorkflowSlug slug, CancellationToken cancellationToken)
    {
        var listed = await workflows.List(includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        return listed.Value.FirstOrDefault(r => r.Slug == slug) switch
        {
            null => SindriErrors.GraphNotFound.With($"no workflow '{slug.Value}'; list the workflows to see what exists"),
            { ArchivedAt: not null } => SindriErrors.GraphArchived.With($"'{slug.Value}' is already archived"),
            var record => record,
        };
    }

    /// <summary>The number of run rows the delete keeps, or the refusal: SND-GRAPH-114 for an active workflow, SND-GRAPH-115 for a run in flight.</summary>
    private async ValueTask<Result<int>> RunsKept(WorkflowRecord record, CancellationToken cancellationToken)
    {
        if (record.ActiveRevision is not null)
        {
            return SindriErrors.GraphActive.With($"'{record.Slug.Value}' is active; pause it first, then delete it");
        }

        var inFlight = await runs.InFlight(record.Id, cancellationToken).ConfigureAwait(false);
        if (inFlight.IsError)
        {
            return inFlight.Error;
        }

        if (inFlight.Value.Count > 0)
        {
            return SindriErrors.GraphRunInFlight.With($"the run '{inFlight.Value[0].Id.Value}' of '{record.Slug.Value}' is still going; delete the workflow after it ends or cancel it");
        }

        var all = await runs.Recent(record.Id, 0, int.MaxValue, cancellationToken).ConfigureAwait(false);
        return all.IsError ? all.Error : all.Value.Count;
    }

    /// <summary>Purges <c lang="text">workflow:slug</c> and every <c lang="text">node:slug/id</c> through the staged purge the purge command uses.</summary>
    private (long Freed, IReadOnlyList<string> Stayed) Purge(string slug)
    {
        var listed = files.List(owner: null);
        if (listed.IsError)
        {
            return (0, [$"workflow:{slug}"]);
        }

        var freed = 0L;
        var stayed = new List<string>();
        var mine = listed.Value.Where(e => Belongs(e.Owner, slug)).ToList();
        foreach (var owner in mine.Select(static e => e.Owner).Distinct())
        {
            if (!TryPurge(owner))
            {
                stayed.Add(owner.ToString());
                continue;
            }

            freed += mine.Where(e => e.Owner == owner).Sum(static e => e.SizeBytes);
        }

        return (freed, stayed);
    }

    private static bool Belongs(ResourceOwner owner, string slug) => owner.Kind switch
    {
        ResourceOwnerKind.Workflow => string.Equals(owner.Key, slug, StringComparison.Ordinal),
        ResourceOwnerKind.Node => owner.Key.StartsWith(slug + "/", StringComparison.Ordinal),
        ResourceOwnerKind.Toolkit => false,
        _ => false,
    };

    private bool TryPurge(ResourceOwner owner)
    {
        var staged = files.Stage(owner);
        return staged.IsSuccess && staged.Value.Delete().IsSuccess;
    }
}
