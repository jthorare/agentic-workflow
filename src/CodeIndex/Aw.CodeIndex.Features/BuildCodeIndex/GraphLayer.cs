using System.Globalization;
using System.Text;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Graph;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Features.BuildCodeIndex;

/// <summary>The graph layer of a build (spec §5.5, §5.7): the tool, if any is registered, over the snapshot's blobs.</summary>
/// <param name="snapshot">The repo's tree at a commit.</param>
/// <param name="clock">The time source.</param>
/// <param name="scrubber">Scrubs each graph symbol's source text before it is stored.</param>
/// <param name="graph">The graph layer's tool, or null where none is registered (the layer is then unavailable).</param>
public sealed class GraphLayer(IRepoSnapshot snapshot, TimeProvider clock, IBodyScrubber scrubber, IGraphRunner? graph = null)
{
    private const int EdgeBatch = 5_000;

    private static readonly string[] _layerCodes = [CodeIndexErrors.NoSandbox.Code, CodeIndexErrors.GraphUnavailable.Code, CodeIndexErrors.PinMismatch.Code];

    /// <summary>
    /// The graph layer (spec §5.5, §5.7): graphify on the whole snapshot, its nodes in files no parser covers stored as graph symbols and its call edges merged
    /// as 'graphify' edges. A tool that is missing, unsandboxed, off the pin or failed leaves the layer unavailable with its code and no stale edge; one that timed out leaves the last good graph in place; only a
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
            return await Failed(build, request, code, ran.Error, cancellationToken).ConfigureAwait(false);
        }

        var merged = await PlanAndStore(build, request, ran.Value, code, cancellationToken).ConfigureAwait(false);
        if (merged.IsError)
        {
            return merged.Error;
        }

        var (planned, unread) = merged.Value;
        var detail = string.Create(CultureInfo.InvariantCulture, $"{planned.Files.Sum(f => f.Symbols.Count)} symbols, {planned.Edges.Count} calls, {ran.Value.Dropped + planned.Dropped} dropped{(unread > 0 ? $", {unread} files without text" : string.Empty)}");
        return new LayerRecord(LayerName.Graph, request.Commit.Value, LayerState.Ok, Code: null, detail, now);
    }

    // A failed run empties the graph, so a layer that is not ok never serves an edge of an earlier build, except a run that ran out of time:
    // that keeps the last good graph and the layer says it is unavailable (owner q93, 2026-10-23).
    private async ValueTask<Result<LayerRecord>> Failed(IIndexBuild build, BuildCodeIndexRequest request, IReadOnlyList<SnapshotFile> code, Error error, CancellationToken cancellationToken)
    {
        if (!IsTimeout(error))
        {
            var emptied = await StoreGraph(build, EmptyGraph(request, code), cancellationToken).ConfigureAwait(false);
            if (emptied.IsError)
            {
                return emptied.Error;
            }
        }

        return Unavailable(error, clock.GetUtcNow());
    }

    private async ValueTask<Result<(GraphMerged Merged, int Unread)>> PlanAndStore(IIndexBuild build, BuildCodeIndexRequest request, GraphSnapshot graphJson, IReadOnlyList<SnapshotFile> code, CancellationToken cancellationToken)
    {
        var planned = GraphMerge.Plan(request.Repo.Name, graphJson, code);
        var withBodies = await AttachBodies(planned, request, cancellationToken).ConfigureAwait(false);
        var changed = await ChangedFiles(build, withBodies.Merged, request.Repo.Settings.Embeddings.Enabled, cancellationToken).ConfigureAwait(false);
        if (changed.IsError)
        {
            return changed.Error;
        }

        var stored = await StoreGraph(build, (graphJson, withBodies.Merged with { Files = changed.Value }), cancellationToken).ConfigureAwait(false);
        return stored.IsError ? stored.Error : withBodies;
    }

    // Writing a file takes its old rows out, and its symbols' vectors with them, so a graph file is written only when its rows would change: it is new, its blob
    // differs, or the symbols planned for it differ from the stored ones (ref, kind, lines, name), or, with embeddings on, one of its stored symbols has neither a body nor a vector (embeddings were off
    // when it was stored, or its blob could not be read). A graph symbol's body is a pure function of its file's text and
    // the symbols' start lines (GraphBodies), so the same blob with the same symbols means the same bodies; they cannot be compared directly, since an embedded symbol's body is dropped.
    private static async ValueTask<Result<IReadOnlyList<FileRecord>>> ChangedFiles(IIndexBuild build, GraphMerged merged, bool embeddings, CancellationToken cancellationToken)
    {
        var files = await build.StoredFiles(cancellationToken).ConfigureAwait(false);
        if (files.IsError)
        {
            return files.Error;
        }

        var symbols = await build.ReadSymbols(cancellationToken).ConfigureAwait(false);
        if (symbols.IsError)
        {
            return symbols.Error;
        }

        var unembeddable = new HashSet<string>(StringComparer.Ordinal);
        if (embeddings)
        {
            var pending = await build.FilesWithoutBodiesOrVectors(cancellationToken).ConfigureAwait(false);
            if (pending.IsError)
            {
                return pending.Error;
            }

            unembeddable.UnionWith(pending.Value);
        }

        var blobs = files.Value.ToDictionary(f => f.Path, f => f.Blob, StringComparer.Ordinal);
        var rows = symbols.Value.ToLookup(s => s.File, s => Shape(s.Ref, s.Kind, s.StartLine, s.EndLine, s.Name), StringComparer.Ordinal);
        List<FileRecord> changed = [.. merged.Files
            .Where(f => unembeddable.Contains(f.Path)
                || !blobs.TryGetValue(f.Path, out var blob)
                || !string.Equals(blob, f.Blob, StringComparison.Ordinal)
                || !rows[f.Path].Order(StringComparer.Ordinal).SequenceEqual(f.Symbols.Select(s => Shape(s.Ref, s.Kind, s.StartLine, s.EndLine, s.Name)).Order(StringComparer.Ordinal), StringComparer.Ordinal))];
        return changed;
    }

    private static string Shape(SymbolRef reference, SymbolKind kind, int start, int end, string name)
        => string.Create(CultureInfo.InvariantCulture, $"{reference.Value}|{kind}|{start}|{end}|{name}");

    // A graph symbol's stored text (the 'bodies' row the embed candidates come from) is its span of the snapshot file, scrubbed and cut; kept only while embeddings
    // are on. A file whose blob cannot be read keeps its symbols without text and is counted, and so is every file when the blobs cannot be opened: the graph's
    // nodes and edges do not depend on the text, so the layer stays ok and says how many files have none.
    private async ValueTask<(GraphMerged Merged, int Unread)> AttachBodies(GraphMerged merged, BuildCodeIndexRequest request, CancellationToken cancellationToken)
    {
        if (!request.Repo.Settings.Embeddings.Enabled || merged.Files.All(f => f.Symbols.Count == 0))
        {
            return (merged, 0);
        }

        var blobs = await snapshot.OpenBlobs(request.Repo, cancellationToken).ConfigureAwait(false);
        if (blobs.IsError)
        {
            return (merged, merged.Files.Count(f => f.Symbols.Count > 0));
        }

        var files = new List<FileRecord>(merged.Files.Count);
        var unread = 0;
        await using (blobs.Value.ConfigureAwait(false))
        {
            foreach (var file in merged.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bodied = file.Symbols.Count == 0 ? file : await AttachBodies(file, blobs.Value, cancellationToken).ConfigureAwait(false);
                unread += bodied is null ? 1 : 0;
                files.Add(bodied ?? file);
            }
        }

        return (merged with { Files = files }, unread);
    }

    // The file with its symbols' bodies, or null when its blob cannot be read or is missing.
    private async ValueTask<FileRecord?> AttachBodies(FileRecord file, IBlobReader reader, CancellationToken cancellationToken)
    {
        var bytes = await reader.Read(file.Blob, cancellationToken).ConfigureAwait(false);
        return bytes is { IsError: false, Value: { } raw }
            ? GraphBodies.Attach(file, Encoding.UTF8.GetString(raw), t => scrubber.Scrub(t, CodeIndexRules.EmbedTextMaxCharacters))
            : null;
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

    // The graph tables and the graphify edges are emptied first, so a layer that is not ok never serves an edge of an earlier build. The files are those to write:
    // every graph file when the layer is not ok, only the changed ones when it is.
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
    private static bool IsTimeout(Error error)
        => string.Equals(error.Code, CodeIndexErrors.GraphUnavailable.Code, StringComparison.Ordinal) && string.Equals(error.Message, "timeout", StringComparison.Ordinal);

    private static LayerRecord Unavailable(Error error, DateTimeOffset now)
    {
        var known = _layerCodes.Contains(error.Code, StringComparer.Ordinal);
        return new LayerRecord(LayerName.Graph, "none", LayerState.Unavailable, known ? error.Code : CodeIndexErrors.GraphUnavailable.Code, known ? error.Message : "failed", now);
    }
}
