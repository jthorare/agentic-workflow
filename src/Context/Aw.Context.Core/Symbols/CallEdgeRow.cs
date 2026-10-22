namespace Aw.Context.Core.Symbols;

/// <summary>One caller or callee of a symbol: the symbol at the other end of a call edge, how sure the index is, and the commit it was read at.</summary>
/// <param name="Symbol">The symbol at the other end of the edge.</param>
/// <param name="Basis">How the edge was found ('semantic', 'syntax', ...), in the index block's words.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Coverage">'full' (a language the index parses itself) or 'partial' (a graph language).</param>
/// <param name="Depth">1 for a direct call, up to 3.</param>
/// <param name="Commit">The commit the index describes.</param>
/// <param name="AsOfCommit">The map version's commit when it differs from <paramref name="Commit"/>, else null.</param>
/// <param name="State">'ok', or 'stale'.</param>
public sealed record CallEdgeRow(SymbolSite Symbol, string Basis, double Confidence, string Coverage, int Depth, string Commit, string? AsOfCommit, SymbolsState State);
