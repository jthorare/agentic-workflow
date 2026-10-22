namespace Aw.Context.Core.Symbols;

/// <summary>A symbol a name search found, with the commit it was read at.</summary>
/// <param name="Site">The symbol.</param>
/// <param name="Commit">The commit the index describes.</param>
/// <param name="AsOfCommit">The map version's commit when it differs from <paramref name="Commit"/> (then <paramref name="State"/> is stale), else null.</param>
/// <param name="State">'ok', or 'stale' when the index is not at the map's commit or not at the head.</param>
public sealed record SymbolHit(SymbolSite Site, string Commit, string? AsOfCommit, SymbolsState State);
