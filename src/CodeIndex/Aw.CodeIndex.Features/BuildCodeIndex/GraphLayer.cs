using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Graph;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Features.BuildCodeIndex;

/// <summary>The graph layer of a build (spec §5.5, §5.7): the tool, if any is registered, over the snapshot's blobs.</summary>
/// <param name="snapshot">The repo's tree at a commit.</param>
/// <param name="clock">The time source.</param>
/// <param name="graph">The graph layer's tool, or null where none is registered (the layer is then unavailable).</param>
public sealed class GraphLayer(IRepoSnapshot snapshot, TimeProvider clock, IGraphRunner? graph = null)
{
    private const int EdgeBatch = 5_000;

    private static readonly string[] _layerCodes = [CodeIndexErrors.NoSandbox.Code, CodeIndexErrors.GraphUnavailable.Code, CodeIndexErrors.PinMismatch.Code];

    /// <summary>
    /// The graph layer (spec §5.5, §5.7): graphify on the whole snapshot, its nodes in files no parser covers stored as graph symbols and its call edges merged
    /// as 'graphify' edges. A tool that is missing, unsandboxed, off the pin or failed leaves the layer unavailable with its code and no stale edge; only a
    /// failure of the index file itself fails the build.
    /// </summary>
    /// <param name="build">The build file.</param>
    /// <param name="request">The build request.</param>
    /// <param name="code">The snapshot's code files.</param>
    /// <param name="cancellationToken">Cancels the layer.</param>
    /// <returns>The layer's row for the caller to record; an error only when the index file cannot be written.</returns>
    public async ValueTask<Result<LayerRecord>> Run(IIndexBuild build, BuildCodeIndexRequest request, IReadOnlyList<SnapshotFile> code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(code);
        var now = clock.GetUtcNow();
        if (!request.Repo.Settings.Graph.Enabled)
        {
            var cleared = await StoreGraph(build, EmptyGraph(request, code), cancellationToken).ConfigureAwait(false);
            return cleared.IsError ? cleared.Error : new LayerRecord(LayerName.Graph, "none", LayerState.Disabled, Code: null, "disabled by the profile", now);
        }

        if (graph is null)
        {
            return new LayerRecord(LayerName.Graph, "none", LayerState.Unavailable, Code: null, "not built by this version", now);
        }

        request.Progress.Report("graph");
        var ran = await RunGraph(graph, request, code, cancellationToken).ConfigureAwait(false);
        if (ran.IsError)
        {
            var emptied = await StoreGraph(build, EmptyGraph(request, code), cancellationToken).ConfigureAwait(false);
            if (emptied.IsError)
            {
                return emptied.Error;
            }

            return Unavailable(ran.Error, now);
        }

        var merged = GraphMerge.Plan(request.Repo.Name, ran.Value, code);
        var stored = await StoreGraph(build, (ran.Value, merged), cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var detail = string.Create(CultureInfo.InvariantCulture, $"{merged.Files.Sum(f => f.Symbols.Count)} symbols, {merged.Edges.Count} calls, {ran.Value.Dropped + merged.Dropped} dropped");
        return new LayerRecord(LayerName.Graph, request.Commit.Value, LayerState.Ok, Code: null, detail, now);
    }

    private async ValueTask<Result<GraphSnapshot>> RunGraph(IGraphRunner tool, BuildCodeIndexRequest request, IReadOnlyList<SnapshotFile> code, CancellationToken cancellationToken)
    {
        var blobs = await snapshot.OpenBlobs(request.Repo, cancellationToken).ConfigureAwait(false);
        if (blobs.IsError)
        {
            return blobs.Error;
        }

        await using (blobs.Value.ConfigureAwait(false))
        {
            return await tool.Run(new GraphRequest(request.Repo.Name, request.Commit, code, blobs.Value, request.Repo.Settings.Graph), cancellationToken).ConfigureAwait(false);
        }
    }

    // The graph tables and the graphify edges are emptied first, so a layer that is not ok never serves an edge of an earlier build.
    private static async ValueTask<Result<Unit>> StoreGraph(IIndexBuild build, (GraphSnapshot Graph, GraphMerged Merged) rows, CancellationToken cancellationToken)
    {
        var (graphJson, merged) = rows;
        var cleared = await build.ClearEdges(EdgeBasis.Graphify, cancellationToken).ConfigureAwait(false);
        if (cleared.IsError)
        {
            return cleared.Error;
        }

        var replaced = await build.ReplaceGraph(graphJson, cancellationToken).ConfigureAwait(false);
        if (replaced.IsError)
        {
            return replaced.Error;
        }

        foreach (var batch in merged.Files.Chunk(CodeIndexRules.FileBatchSize))
        {
            var written = await build.WriteFiles(batch, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written.Error;
            }
        }

        foreach (var batch in merged.Edges.Chunk(EdgeBatch))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var added = await build.AddEdges(batch, cancellationToken).ConfigureAwait(false);
            if (added.IsError)
            {
                return added.Error;
            }
        }

        return Unit.Value;
    }

    // The graph of a layer that is not ok: no nodes, no edges, and every non-C# code file written without graph symbols, so none of an earlier build lingers.
    private static (GraphSnapshot, GraphMerged) EmptyGraph(BuildCodeIndexRequest request, IReadOnlyList<SnapshotFile> code)
    {
        var empty = new GraphSnapshot([], []);
        return (empty, GraphMerge.Plan(request.Repo.Name, empty, code));
    }

    // Only the three layer codes and their closed-set detail reach the layer row; any other failure reads as 'failed'.
    private static LayerRecord Unavailable(Error error, DateTimeOffset now)
    {
        var known = _layerCodes.Contains(error.Code, StringComparer.Ordinal);
        return new LayerRecord(LayerName.Graph, "none", LayerState.Unavailable, known ? error.Code : CodeIndexErrors.GraphUnavailable.Code, known ? error.Message : "failed", now);
    }
}
