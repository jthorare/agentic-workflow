using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Symbols;

/// <summary>
/// Symbols, callers and callees from the code index block (code index spec §7.2). Context never sees the index's file, SQLite or paths: Sindri's Host adapts the block's facade to this port.
/// Every call takes 'asOf', the commit the map version recorded for the repo (or null), and the rows say when the index is at another commit: 'asOfCommit' set and 'state' 'stale', computed at
/// read time (<see cref="CodeIndexReading.Stamp"/>), so they turn fresh by themselves once the index for the map's commit lands. A repo without a readable index is an answer
/// (<see cref="CodeIndexState.Missing"/>, no rows), never an error; an error is a read that failed.
/// </summary>
public interface ICodeSymbolSource
{
    /// <summary>The commit the index describes for a repo, from stored state only, and how it compares with <paramref name="asOf"/>.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="asOf">The map version's commit, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The head; an error only when the read failed.</returns>
    public ValueTask<Result<CodeIndexHead>> Heads(ContextRepoName repo, string? asOf, CancellationToken cancellationToken);

    /// <summary>The symbols a search finds, ordered, at most <see cref="SymbolQuery.Max"/>.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="query">The search.</param>
    /// <param name="asOf">The map version's commit, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The hits under their head.</returns>
    public ValueTask<Result<SymbolAnswer<SymbolHit>>> FindSymbols(ContextRepoName repo, SymbolQuery query, string? asOf, CancellationToken cancellationToken);

    /// <summary>The symbols each of up to 200 files defines (a file the index does not know has no row).</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="paths">The repo-relative paths.</param>
    /// <param name="asOf">The map version's commit, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One row per known file, in the order asked.</returns>
    public ValueTask<Result<SymbolAnswer<FileSymbolsRow>>> FileSymbols(ContextRepoName repo, IReadOnlyList<string> paths, string? asOf, CancellationToken cancellationToken);

    /// <summary>The edges into a symbol, depth 1 to 3.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="symbolRef">The symbol's ref ('path#name').</param>
    /// <param name="depth">1 to 3.</param>
    /// <param name="asOf">The map version's commit, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The callers and how many call sites resolved to nothing.</returns>
    public ValueTask<Result<CallAnswer>> Callers(ContextRepoName repo, string symbolRef, int depth, string? asOf, CancellationToken cancellationToken);

    /// <summary>The edges out of a symbol, depth 1 to 3.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="symbolRef">The symbol's ref ('path#name').</param>
    /// <param name="depth">1 to 3.</param>
    /// <param name="asOf">The map version's commit, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The callees and how many call sites resolved to nothing.</returns>
    public ValueTask<Result<CallAnswer>> Callees(ContextRepoName repo, string symbolRef, int depth, string? asOf, CancellationToken cancellationToken);
}
