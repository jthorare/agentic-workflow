namespace Aw.Context.Core.Concepts;

/// <summary>
/// Folds near-duplicate suggested concepts into one (issue 2975): two suggestions are one thing when they share most of their terms or most of their code files. "Most" is at least
/// <see cref="MinShared"/> items and at least <see cref="MostShared"/> of the smaller set, so two concepts that share one common word ('tab') stay apart. The concept with the most
/// members leads (the earlier one on a tie) and keeps its place; every later suggestion that overlaps the lead itself joins it, and its name goes on the lead's "also" list. A pin
/// never takes part: the owner decided it.
/// </summary>
internal static class ConceptMerge
{
    /// <summary>The fewest shared terms or code files that can make two suggestions one.</summary>
    internal const int MinShared = 2;

    /// <summary>The share of the smaller set two suggestions must have in common: 0.6 (3 of 5 terms).</summary>
    internal const double MostShared = 0.6;

    /// <summary>Folds the concepts, in the order given.</summary>
    /// <param name="concepts">The concepts in answer order.</param>
    /// <returns>The concepts that stay, in the same order, each with the ids it took in (empty when none).</returns>
    internal static IReadOnlyList<(string Id, IReadOnlyList<string> Also)> Fold(IReadOnlyList<ConceptTraits> concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);
        var taken = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var joined = new HashSet<string>(StringComparer.Ordinal);
        foreach (var lead in concepts.Select((c, i) => (Concept: c, Index: i)).Where(c => !c.Concept.Pinned).OrderByDescending(c => c.Concept.Members).ThenBy(c => c.Index).Select(c => c.Concept))
        {
            if (joined.Contains(lead.Id))
            {
                continue;
            }

            var also = concepts.Where(c => !c.Pinned && !joined.Contains(c.Id) && !string.Equals(c.Id, lead.Id, StringComparison.Ordinal) && !taken.ContainsKey(c.Id) && Overlaps(lead, c)).Select(c => c.Id).ToList();
            joined.UnionWith(also);
            taken[lead.Id] = also;
        }

        return [.. concepts.Where(c => !joined.Contains(c.Id)).Select(c => (c.Id, Also(taken, c.Id)))];
    }

    /// <summary>Whether two suggestions share most of their terms or most of their code files.</summary>
    /// <param name="a">One concept.</param>
    /// <param name="b">The other.</param>
    /// <returns>True when they are one thing.</returns>
    internal static bool Overlaps(ConceptTraits a, ConceptTraits b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return Most(a.Terms, b.Terms, StringComparer.OrdinalIgnoreCase) || Most(a.CodeFiles, b.CodeFiles, StringComparer.Ordinal);
    }

    private static bool Most(IReadOnlyList<string> a, IReadOnlyList<string> b, StringComparer comparer)
    {
        var (left, right) = (a.ToHashSet(comparer), b.ToHashSet(comparer));
        var shared = left.Count(right.Contains);
        return shared >= MinShared && shared >= MostShared * Math.Min(left.Count, right.Count);
    }

    private static List<string> Also(Dictionary<string, List<string>> taken, string id) => taken.TryGetValue(id, out var also) ? also : [];
}
