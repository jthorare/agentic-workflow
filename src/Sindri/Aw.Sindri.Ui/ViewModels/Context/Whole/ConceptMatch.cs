namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>Picks the concept a page's name points at: the one that shares the most words with it, case and plural ignored; a tie goes to the concept with more pages, then to the first.</summary>
internal static class ConceptMatch
{
    private static readonly string[] _suffixes = ["ing", "ies", "es", "ed", "ly", "s"];

    /// <summary>Finds the concept a page name points at.</summary>
    /// <param name="pageName">The page's name.</param>
    /// <param name="known">The concepts, in the Context page's order.</param>
    /// <returns>The concept, or null when no word is shared.</returns>
    internal static ConceptChoice? Best(string pageName, IReadOnlyList<ConceptChoice> known)
    {
        ArgumentNullException.ThrowIfNull(known);
        var words = Words(pageName);
        return known.Select(c => (Concept: c, Shared: Words(c.Name).Intersect(words, StringComparer.Ordinal).Count()))
            .Where(static m => m.Shared > 0)
            .OrderByDescending(static m => m.Shared)
            .ThenByDescending(static m => m.Concept.Pages)
            .Select(static m => m.Concept)
            .FirstOrDefault();
    }

    private static HashSet<string> Words(string name) =>
        [.. name.Split([' ', '-', '_', '/', ':', '.', ','], StringSplitOptions.RemoveEmptyEntries).Select(Stem)];

    // A word's stem: lower case, one common ending dropped (when four letters stay) and a trailing e, so "Schedule", "Scheduling" and "schedules" are one word.
    private static string Stem(string word)
    {
        var stem = word.ToLowerInvariant();
        var suffix = _suffixes.FirstOrDefault(s => stem.EndsWith(s, StringComparison.Ordinal) && stem.Length - s.Length >= 4);
        stem = suffix is null ? stem : stem[..^suffix.Length];
        return stem.Length > 4 && stem.EndsWith('e') ? stem[..^1] : stem;
    }
}
