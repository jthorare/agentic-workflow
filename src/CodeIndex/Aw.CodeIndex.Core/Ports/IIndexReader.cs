using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>
/// A read-only view of one published index file; every query is parameterized and nothing a caller passes becomes SQL or a
/// path. Held only for one facade call.
/// </summary>
public interface IIndexReader : IAsyncDisposable
{
    /// <summary>The header read when the file was opened.</summary>
    public IndexHeader Header { get; }

    /// <summary>Finds symbols by exact name, path, 'path#name' or ref, or by prefix (a range scan, never LIKE with caller text).</summary>
    /// <param name="query">The search; the reader ignores its Repo.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Ordered hits, at most the query's limit.</returns>
    public ValueTask<Result<SymbolPage>> FindSymbols(SymbolQuery query, CancellationToken cancellationToken);

    /// <summary>The symbols each file defines.</summary>
    /// <param name="paths">The paths.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One entry per path, in line order within each.</returns>
    public ValueTask<Result<IReadOnlyList<FileSymbolsEntry>>> FileSymbols(IReadOnlyList<string> paths, CancellationToken cancellationToken);

    /// <summary>Walks the edges into or out of a symbol.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="direction">Callers or callees.</param>
    /// <param name="depth">1 to 8 levels, at most 1,000 edges each.</param>
    /// <param name="includeImplementations">Follow 'inherits' edges from an interface or base member to its implementations.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The walk; SND-CODEIDX-011 when the ref names no symbol.</returns>
    public ValueTask<Result<CallWalk>> Walk(SymbolRef symbol, CallDirection direction, CallDepth depth, bool includeImplementations, CancellationToken cancellationToken);

    /// <summary>Finds a symbol's clone candidates: the same shape hash, and the symbols sharing a MinHash band.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The candidates; SND-CODEIDX-011 when the ref names no symbol.</returns>
    public ValueTask<Result<CloneCandidates>> CloneCandidates(SymbolRef symbol, CancellationToken cancellationToken);

    /// <summary>The nearest stored vectors to a vector.</summary>
    /// <param name="vector">The query vector.</param>
    /// <param name="k">How many.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Nearest first.</returns>
    public ValueTask<Result<IReadOnlyList<Neighbor>>> Nearest(ReadOnlyMemory<float> vector, int k, CancellationToken cancellationToken);

    /// <summary>The stored vectors of every symbol in the files, for one embedding model.</summary>
    /// <param name="paths">Repo-relative paths, distinct.</param>
    /// <param name="model">Only rows embedded with this model count.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The embedded symbols by path then line; a path with none contributes nothing.</returns>
    public ValueTask<Result<IReadOnlyList<SymbolVector>>> SymbolVectors(IReadOnlyList<string> paths, string model, CancellationToken cancellationToken);
}
