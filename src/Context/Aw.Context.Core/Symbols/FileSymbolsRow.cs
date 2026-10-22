namespace Aw.Context.Core.Symbols;

/// <summary>The symbols one file defines, with the commit they were read at.</summary>
/// <param name="Path">The file.</param>
/// <param name="Symbols">Its symbols, in file order.</param>
/// <param name="Commit">The commit the index describes.</param>
/// <param name="AsOfCommit">The map version's commit when it differs from <paramref name="Commit"/>, else null.</param>
/// <param name="State">'ok', or 'stale'.</param>
public sealed record FileSymbolsRow(string Path, IReadOnlyList<SymbolSite> Symbols, string Commit, string? AsOfCommit, SymbolsState State);
