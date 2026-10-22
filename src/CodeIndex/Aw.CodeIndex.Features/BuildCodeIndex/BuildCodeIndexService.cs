using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Planning;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.CodeIndex.Features.BuildCodeIndex;

/// <summary>
/// The build slice (spec §5.7): a heavy slot, the snapshot, the incremental plan, the structure write in batches, the syntax
/// edges and the publish. The graph layer (graphify) runs after the syntax edges and never fails the build; the clones rows are written with the structure; the embeddings layer is built here; the deps layer plugs in later; until then it is recorded as unavailable
/// with its reason, so a reader never mistakes a layer that was not built for one that is empty.
/// </summary>
/// <param name="heads">Whether a commit is still on the default branch.</param>
/// <param name="snapshot">The repo's tree at a commit.</param>
/// <param name="structure">Parses and writes the planned files.</param>
/// <param name="layers">The embeddings and graph layers.</param>
/// <param name="store">The index files.</param>
/// <param name="heavy">The box-wide heavy-job slots.</param>
/// <param name="clock">The time source.</param>
public sealed partial class BuildCodeIndexService(IRepoHeads heads, IRepoSnapshot snapshot, StructureWriter structure, BuildLayers layers, IIndexStore store, IHeavyJobLock heavy, TimeProvider clock) : IService<BuildCodeIndexRequest, BuildReport>
{
    private static readonly LayerName[] _laterLayers = [LayerName.Deps];

    /// <summary>A build in progress: its file, its request and what the snapshot found.</summary>
    private sealed record BuildContext(IIndexBuild Build, BuildCodeIndexRequest Request, SnapshotInventory Inventory, string DefaultBranch, long Started);

    /// <inheritdoc />
    public async ValueTask<Result<BuildReport>> Handle(BuildCodeIndexRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = clock.GetTimestamp();
        var slot = await heavy.Take("sindri code-index:" + request.Repo.Name.Value, CodeIndexRules.HeavySlotWait, waiting: null, cancellationToken).ConfigureAwait(false);
        if (slot.IsError)
        {
            return string.Equals(slot.Error.Code, CodeIndexErrors.HeavyBusy.Code, StringComparison.Ordinal) ? Nothing(request, BuildOutcome.Deferred, started) : slot.Error;
        }

        await using (slot.Value.ConfigureAwait(false))
        {
            return await Run(request, started, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Result<BuildReport>> Run(BuildCodeIndexRequest request, long started, CancellationToken cancellationToken)
    {
        var (repo, commit) = (request.Repo, request.Commit);
        var onMain = await heads.OnDefaultBranch(repo, commit, cancellationToken).ConfigureAwait(false);
        if (onMain.IsError)
        {
            return onMain.Error;
        }

        if (!onMain.Value.Contains)
        {
            return Nothing(request, BuildOutcome.NotOnMain, started);
        }

        var inventory = await snapshot.Inventory(repo, commit, repo.Settings.SnapshotLimits(), cancellationToken).ConfigureAwait(false);
        if (inventory.IsError)
        {
            return inventory.Error;
        }

        var begun = await store.BeginBuild(repo.Name, incremental: true, cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            return await Build(new BuildContext(begun.Value, request, inventory.Value, onMain.Value.DefaultBranch, started), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Result<BuildReport>> Build(BuildContext context, CancellationToken cancellationToken)
    {
        var (build, request) = (context.Build, context.Request);
        var settings = request.Repo.Settings;
        var stamp = StructureStamp.Compute(structure.ParserVersion, settings.UtilityGlobs, []);
        var code = context.Inventory.Files.Where(f => LanguageTable.Of(f.Path) is not null).ToList();
        var stored = await build.StoredFiles(cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var plan = BuildPlanner.Plan(new BuildPlanInput(code, stored.Value, build.Previous?.Layers ?? [], new Dictionary<LayerName, string> { [LayerName.Structure] = stamp }));
        structure.Prepare(request.Repo.Name, code.Select(f => f.Blob).ToHashSet(StringComparer.Ordinal));
        var wrote = await structure.Write(build, request, plan, cancellationToken).ConfigureAwait(false);
        if (wrote.IsError)
        {
            return wrote.Error;
        }

        var edges = await WriteEdges(build, request, ProjectManifests.Directories(context.Inventory.Files.Select(f => f.Path)), cancellationToken).ConfigureAwait(false);
        if (edges.IsError)
        {
            return edges.Error;
        }

        var graphLayer = await layers.Graph.Run(build, request, code, cancellationToken).ConfigureAwait(false);
        if (graphLayer.IsError)
        {
            return graphLayer.Error;
        }

        var embedded = await layers.Embeddings.Run(build, settings.Embeddings, request.Progress, cancellationToken).ConfigureAwait(false);
        if (embedded.IsError)
        {
            return embedded.Error;
        }

        var recorded = await RecordLayers(build, stamp, graphLayer.Value, cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : await Publish(context, edges.Value, embedded.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<BuildReport>> Publish(BuildContext context, EdgeShares shares, EmbeddingProgress embedded, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = context.Request;
        var meta = new IndexMeta(request.Commit, context.DefaultBranch, clock.GetUtcNow(), CodeIndexRules.SchemaVersion, structure.ParserVersion, LockSha: null, new IndexCounts(0, 0, 0));
        var published = await context.Build.Publish(meta, cancellationToken).ConfigureAwait(false);
        if (published.IsError)
        {
            return published.Error;
        }

        request.Progress.Report("published");
        return new BuildReport(
            request.Repo.Name,
            request.Commit,
            BuildOutcome.Published,
            published.Value.Meta.Counts,
            [.. published.Value.Layers.Select(l => new LayerStatus(l.Layer, l.State, l.Code, l.Detail))],
            shares.UnresolvedShare,
            shares.AmbiguousShare,
            embedded.Embedded,
            embedded.Embeddable,
            context.Build.Incremental,
            Elapsed(context.Started));
    }

    private async ValueTask<Result<Unit>> RecordLayers(IIndexBuild build, string stamp, LayerRecord graphLayer, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var first = await build.SetLayer(new LayerRecord(LayerName.Structure, stamp, LayerState.Ok, Code: null, "ok", now), cancellationToken).ConfigureAwait(false);
        if (first.IsError)
        {
            return first.Error;
        }

        var graphRecorded = await build.SetLayer(graphLayer, cancellationToken).ConfigureAwait(false);
        if (graphRecorded.IsError)
        {
            return graphRecorded.Error;
        }

        // Clone rows are written with each file's symbols, so the layer is current whenever the structure is.
        var clones = await build.SetLayer(new LayerRecord(LayerName.Clones, stamp, LayerState.Ok, Code: null, "ok", now), cancellationToken).ConfigureAwait(false);
        if (clones.IsError)
        {
            return clones.Error;
        }

        foreach (var layer in _laterLayers)
        {
            var recorded = await build.SetLayer(new LayerRecord(layer, "none", LayerState.Unavailable, Code: null, "not built by this version", now), cancellationToken).ConfigureAwait(false);
            if (recorded.IsError)
            {
                return recorded.Error;
            }
        }

        return Unit.Value;
    }

    private BuildReport Nothing(BuildCodeIndexRequest request, BuildOutcome outcome, long started)
        => new(request.Repo.Name, Commit: null, outcome, new IndexCounts(0, 0, 0), [], UnresolvedShare: 0, AmbiguousShare: 0, Embedded: 0, Embeddable: 0, Incremental: false, Elapsed(started));

    private long Elapsed(long started) => (long)clock.GetElapsedTime(started).TotalMilliseconds;

}
