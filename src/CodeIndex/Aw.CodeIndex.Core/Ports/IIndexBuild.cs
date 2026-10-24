using Aw.CodeIndex.Core.Model;
using Aw.Kernel;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>
/// One build file in progress (spec §4.1, §5.7): the only way to write an index. Dispose without publishing deletes the
/// file and leaves the live index alone.
/// </summary>
public interface IIndexBuild : IAsyncDisposable
{
    /// <summary>True when this build started from a copy of the live index; false for a first build or one that started over (integrity check failed, SND-CODEIDX-010, or another schema).</summary>
    public bool Incremental { get; }

    /// <summary>The header of the index this build copied, or null when it started from scratch; the planner's stamps come from it.</summary>
    public IndexHeader? Previous { get; }

    /// <summary>The file rows the build file holds.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Path, blob and parsed flag per file.</returns>
    public ValueTask<Result<IReadOnlyList<StoredFile>>> StoredFiles(CancellationToken cancellationToken);

    /// <summary>Writes files with their symbols, call sites, bands and bodies in one transaction, replacing any rows of the same paths (a file's old symbols cascade away).</summary>
    /// <param name="files">A batch; the build writes 200 files at a time.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> WriteFiles(IReadOnlyList<FileRecord> files, CancellationToken cancellationToken);

    /// <summary>Drops files that left the snapshot, with their symbols and call sites.</summary>
    /// <param name="paths">The paths.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> RemoveFiles(IReadOnlyList<string> paths, CancellationToken cancellationToken);

    /// <summary>Replaces every dependency row.</summary>
    /// <param name="deps">The rows.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> ReplaceDeps(IReadOnlyList<DepRecord> deps, CancellationToken cancellationToken);

    /// <summary>Every symbol, as the resolver needs it.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The symbols.</returns>
    public ValueTask<Result<IReadOnlyList<ResolutionSymbol>>> ReadSymbols(CancellationToken cancellationToken);

    /// <summary>Every call site, streamed.</summary>
    /// <param name="cancellationToken">Cancels the stream.</param>
    /// <returns>The call sites with their callers.</returns>
    public IAsyncEnumerable<CallSiteRow> ReadCallSites(CancellationToken cancellationToken);

    /// <summary>Deletes every edge of a basis, so a re-resolve starts clean.</summary>
    /// <param name="basis">Syntax or graphify.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> ClearEdges(EdgeBasis basis, CancellationToken cancellationToken);

    /// <summary>Adds edges; an edge identical to a stored one (source, target name, kind, basis, line and target) is ignored.</summary>
    /// <param name="edges">The edges.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> AddEdges(IReadOnlyList<EdgeRecord> edges, CancellationToken cancellationToken);

    /// <summary>Empties the graph tables and fills them from a run.</summary>
    /// <param name="graph">The checked graph; an empty one when the layer is not ok.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> ReplaceGraph(GraphSnapshot graph, CancellationToken cancellationToken);

    /// <summary>The symbols still to embed, with their stored text (class-like symbols included).</summary>
    /// <param name="limit">At most this many.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The candidates, in a stable order.</returns>
    public ValueTask<Result<IReadOnlyList<EmbedCandidate>>> EmbedCandidates(int limit, CancellationToken cancellationToken);

    /// <summary>Stores vectors and drops the stored body of each embedded symbol.</summary>
    /// <param name="vectors">The vectors.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> WriteEmbeddings(IReadOnlyList<EmbeddingRecord> vectors, CancellationToken cancellationToken);

    /// <summary>How many symbols have a vector and how many should.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The counts.</returns>
    public ValueTask<Result<EmbeddingProgress>> CountEmbeddings(CancellationToken cancellationToken);

    /// <summary>Records a layer's row.</summary>
    /// <param name="layer">The row.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> SetLayer(LayerRecord layer, CancellationToken cancellationToken);

    /// <summary>Finishes the build: writes the meta (counting files, symbols and edges itself; the counts in <paramref name="meta"/> are ignored), checks integrity, leaves the file immutable with no sidecars and swaps the pointer.</summary>
    /// <param name="meta">The header.</param>
    /// <param name="cancellationToken">Cancels the call; a cancel before the swap leaves the live index alone.</param>
    /// <returns>The published header; SND-CODEIDX-010 when the integrity check fails (nothing is published).</returns>
    public ValueTask<Result<IndexHeader>> Publish(IndexMeta meta, CancellationToken cancellationToken);
}
