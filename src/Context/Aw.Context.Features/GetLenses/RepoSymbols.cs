using Aw.Context.Core.Lenses;
using Aw.Context.Core.Symbols;

namespace Aw.Context.Features.GetLenses;

/// <summary>
/// Joins the symbol columns read from several repos into the code band's one column (#3776). The choice: the band's state is the worst of the repos read
/// (unavailable, then stale, then ok), its reason and code are the worst repo's own (so "index not built" is only ever said by a repo whose index is not built),
/// and the files, implements and items of every repo that answered are kept. The commit pair is kept only when one repo was read, since two repos have two commits.
/// An unavailable band keeps the names of the repos at its state and reason (#3861: each column read with several repos in scope carries its repo); a band that answers names none.
/// </summary>
internal static class RepoSymbols
{
    /// <summary>Joins the columns, one per repo read.</summary>
    /// <param name="columns">The columns; at least one.</param>
    /// <returns>The joined column.</returns>
    internal static LensSymbols Join(IReadOnlyList<LensSymbols> columns)
    {
        if (columns.Count == 1)
        {
            return columns[0].State == SymbolsState.Unavailable ? columns[0] : columns[0] with { Repos = null };
        }

        var worst = columns.OrderBy(c => c.State).First();
        var answered = columns.Where(c => c.State != SymbolsState.Unavailable).ToList();
        return worst with
        {
            Repos = worst.State == SymbolsState.Unavailable ? Named(columns.Where(c => c.State == worst.State && string.Equals(c.Reason, worst.Reason, StringComparison.Ordinal))) : null,
            Items = [.. answered.SelectMany(c => c.Items)],
            Files = Concat(answered.Select(c => c.Files)),
            Implements = Concat(answered.Select(c => c.Implements)),
            Commit = null,
            AsOfCommit = null,
            AsOfMismatch = columns.Any(c => c.AsOfMismatch),
        };
    }

    private static List<string>? Named(IEnumerable<LensSymbols> columns)
    {
        var names = columns.SelectMany(c => c.Repos ?? []).Distinct(StringComparer.Ordinal).ToList();
        return names.Count == 0 ? null : names;
    }

    private static IReadOnlyList<T>? Concat<T>(IEnumerable<IReadOnlyList<T>?> parts)
    {
        var present = parts.OfType<IReadOnlyList<T>>().ToList();
        return present.Count == 0 ? null : [.. present.SelectMany(p => p)];
    }
}
