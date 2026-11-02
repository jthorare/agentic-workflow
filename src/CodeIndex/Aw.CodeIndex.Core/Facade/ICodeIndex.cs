using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>
/// The code index's one facade (spec §7): every surface calls it with Core records only. Reads open the repo's index
/// file read-only and never create it. Every call takes a <see cref="CodeIndexScope"/> and returns a <see cref="Result{T}"/>;
/// a repo outside the scope, or no such repo, is the same not-found as a repo with no index (SND-CODEIDX-013).
/// <see cref="Build"/> and <see cref="Embed"/> are the only writes.
/// </summary>
public interface ICodeIndex
{
    /// <summary>Builds or updates a repo's index at a commit (spec §5.7); called by Sindri's BuildCodeIndexService.</summary>
    /// <param name="scope">The caller's repos.</param>
    /// <param name="repo">The repo to build.</param>
    /// <param name="commit">The commit; it must still be on the default branch.</param>
    /// <param name="progress">Told a counts-only line per step ('structure 1,400 of 3,100 files').</param>
    /// <param name="cancellationToken">Cancels the build between files and batches; a cancelled build leaves the live index alone.</param>
    /// <returns>The report. A build takes no heavy-job slot; SND-CODEIDX-002, 003, 010 and 011 are errors.</returns>
    public ValueTask<Result<BuildReport>> Build(CodeIndexScope scope, RepoName repo, CommitSha commit, IProgress<string> progress, CancellationToken cancellationToken);

    /// <summary>Embeds a repo's pending symbols without the heavy slot, until none remain or the model fails (spec §5.7a); called by Sindri's EmbedCodeIndexService.</summary>
    /// <param name="scope">The caller's repos.</param>
    /// <param name="repo">The repo to embed.</param>
    /// <param name="progress">Told a counts-only line per batch ('embeddings 1,400 of 3,100').</param>
    /// <param name="cancellationToken">Cancels the run between batches; every published chunk is kept.</param>
    /// <returns>Where the embeddings stand. SND-CODEIDX-016 (a build holds the repo's index) and 013 (no index yet) mean try later; 005, 006 and 009 are the model's failures.</returns>
    public ValueTask<Result<EmbedReport>> Embed(CodeIndexScope scope, RepoName repo, IProgress<string> progress, CancellationToken cancellationToken);

    /// <summary>Each repo's index state from the files only: no git, no counting.</summary>
    /// <param name="scope">The caller's repos.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One row per repo in scope.</returns>
    public ValueTask<Result<StatusAnswer>> Status(CodeIndexScope scope, CancellationToken cancellationToken);

    /// <summary>The commit each index describes, from stored state only; the service's recorded heads.</summary>
    /// <param name="scope">The caller's repos.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One entry per repo in scope; a missing or damaged index has a null commit.</returns>
    public ValueTask<Result<IReadOnlyList<RecordedHead>>> Heads(CodeIndexScope scope, CancellationToken cancellationToken);

    /// <summary>Which unavailable layers can be built now: the same lock and tool check the build does, no work.</summary>
    /// <param name="scope">The caller's repos.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One entry per repo in scope.</returns>
    public ValueTask<Result<IReadOnlyList<LayerProbe>>> ProbeLayers(CodeIndexScope scope, CancellationToken cancellationToken);

    /// <summary>Finds symbols by exact name, path, 'path#name' or ref, or by prefix.</summary>
    /// <param name="scope">The caller's repos.</param>
    /// <param name="query">The search.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Ordered hits; SND-CODEIDX-012 for an empty or over-limit query, 013 when no index.</returns>
    public ValueTask<Result<FindSymbolsAnswer>> FindSymbols(CodeIndexScope scope, SymbolQuery query, CancellationToken cancellationToken);

    /// <summary>The symbols each of up to 200 files defines.</summary>
    /// <param name="scope">The caller's repos.</param>
    /// <param name="repo">The repo.</param>
    /// <param name="paths">Repo-relative paths.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One entry per path; SND-CODEIDX-012 over 200 paths.</returns>
    public ValueTask<Result<FileSymbolsAnswer>> FileSymbols(CodeIndexScope scope, RepoName repo, IReadOnlyList<string> paths, CancellationToken cancellationToken);

    /// <summary>The edges into a symbol, depth 1 to 3, at most 100 per level.</summary>
    /// <param name="scope">The caller's repos.</param>
    /// <param name="symbol">The symbol; the repo inside it is scope-checked.</param>
    /// <param name="depth">How many levels.</param>
    /// <param name="includeImplementations">Follow 'inherits' edges from an interface or base member to its implementations.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The edges and the unresolved count; SND-CODEIDX-011 for a ref that names no symbol.</returns>
    public ValueTask<Result<CallsAnswer>> Callers(CodeIndexScope scope, SymbolRef symbol, CallDepth depth, bool includeImplementations, CancellationToken cancellationToken);

    /// <summary>The edges out of a symbol, same shape as <see cref="Callers"/>.</summary>
    /// <param name="scope">The caller's repos.</param>
    /// <param name="symbol">The symbol.</param>
    /// <param name="depth">How many levels.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The edges and the unresolved count; SND-CODEIDX-011 for a ref that names no symbol.</returns>
    public ValueTask<Result<CallsAnswer>> Callees(CodeIndexScope scope, SymbolRef symbol, CallDepth depth, CancellationToken cancellationToken);

    /// <summary>Exact clones (same shape hash) and near clones (MinHash bands, then estimated Jaccard at or above the threshold).</summary>
    /// <param name="scope">The caller's repos.</param>
    /// <param name="symbol">The symbol.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The clones; SND-CODEIDX-011 for a ref that names no symbol.</returns>
    public ValueTask<Result<SimilarAnswer>> Similar(CodeIndexScope scope, SymbolRef symbol, CancellationToken cancellationToken);

    /// <summary>The k nearest symbols to a text by embedding; the text is embedded by the same loopback model.</summary>
    /// <param name="scope">The caller's repos.</param>
    /// <param name="repo">The repo.</param>
    /// <param name="text">The text.</param>
    /// <param name="k">How many, 1 or more.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The neighbors; unavailable with the embeddings layer's code when that layer is (006, 009, 005).</returns>
    public ValueTask<Result<NeighborsAnswer>> Neighbors(CodeIndexScope scope, RepoName repo, string text, int k, CancellationToken cancellationToken);

    /// <summary>The stored vectors of every embedded symbol in up to 2,000 files; read-only, it never embeds and never calls the model.</summary>
    /// <param name="scope">The caller's repos.</param>
    /// <param name="repo">The repo.</param>
    /// <param name="query">The repo-relative paths.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The vectors of the index's current embedding model and the paths with none; SND-CODEIDX-012 for no path or over 2,000, 013 when no index or the layer is off, the layer's own code when it is unavailable (006, 009, 005).</returns>
    public ValueTask<Result<SymbolVectorsView>> SymbolVectors(CodeIndexScope scope, RepoName repo, SymbolVectorsQuery query, CancellationToken cancellationToken);
}
