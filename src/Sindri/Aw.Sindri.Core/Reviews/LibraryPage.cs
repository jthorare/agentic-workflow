using System.Globalization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The library page's body (PR review page spec §6.1): counts, the docs with errors and the near misses, each list cut to <see cref="Cap"/>.</summary>
/// <param name="Docs">The design docs stored.</param>
/// <param name="WithErrors">The docs stored with an error, however many are listed.</param>
/// <param name="Broken">The first <see cref="Cap"/> docs with errors.</param>
/// <param name="NearMisses">The first <see cref="Cap"/> near misses.</param>
/// <param name="UnreadableRoots">The folders that couldn't be read.</param>
/// <param name="CappedText">Sindri's words when a list was cut; null otherwise.</param>
public sealed record LibraryPage(int Docs, int WithErrors, IReadOnlyList<LibraryBrokenDoc> Broken, IReadOnlyList<LibraryNearMiss> NearMisses, IReadOnlyList<ReviewUnreadableRoot> UnreadableRoots, string? CappedText)
{
    /// <summary>The most docs with errors (and the most near misses) a page lists.</summary>
    public const int Cap = 200;

    /// <summary>Cuts the facts to the cap, keeping their order.</summary>
    /// <param name="facts">What the library holds.</param>
    /// <returns>The page.</returns>
    public static LibraryPage From(LibraryPageFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var notes = new List<string>();
        if (facts.Broken.Count > Cap)
        {
            notes.Add(string.Create(CultureInfo.InvariantCulture, $"Showing the first {Cap} of {facts.Broken.Count} docs with errors; run `sindri library validate` for the rest"));
        }

        if (facts.NearMisses.Count > Cap)
        {
            notes.Add(string.Create(CultureInfo.InvariantCulture, $"Showing the first {Cap} of {facts.NearMisses.Count} near misses; run `sindri library validate` for the rest"));
        }

        return new LibraryPage(facts.Docs, facts.Broken.Count, [.. facts.Broken.Take(Cap)], [.. facts.NearMisses.Take(Cap)], facts.UnreadableRoots, notes.Count == 0 ? null : string.Join(". ", notes));
    }
}
