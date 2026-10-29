namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>Matches each fresh group to the stored concept it continues, so a relabel keeps its key.</summary>
public static class ConceptMatcher
{
    /// <summary>
    /// A group with the same membership as a stored concept is that concept. Otherwise the stored concept that shares the most records with it is (at least one, and a concept
    /// continues in one group only: the biggest overlap wins, ties go to the earlier group, then the lower key). A group nothing matches is a new concept.
    /// </summary>
    /// <param name="groups">The groups, in grouper order.</param>
    /// <param name="stored">The stored concepts.</param>
    /// <returns>One plan per group, in the groups' order.</returns>
    public static IReadOnlyList<ConceptPlan> Plan(IReadOnlyList<ConceptGroup> groups, IReadOnlyList<SynthesizedConcept> stored)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(stored);
        var matched = new SynthesizedConcept?[groups.Count];
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        MatchSame(groups, stored, matched, claimed);
        MatchOverlapping(groups, stored, matched, claimed);

        return [.. groups.Select((g, i) => new ConceptPlan(g, matched[i]))];
    }

    private static void MatchSame(IReadOnlyList<ConceptGroup> groups, IReadOnlyList<SynthesizedConcept> stored, SynthesizedConcept?[] matched, HashSet<string> claimed)
    {
        for (var i = 0; i < groups.Count; i++)
        {
            var same = stored.FirstOrDefault(c => !claimed.Contains(c.Key) && string.Equals(c.MembershipHash, groups[i].MembershipHash, StringComparison.Ordinal));
            if (same is not null)
            {
                matched[i] = same;
                _ = claimed.Add(same.Key);
            }
        }
    }

    private static void MatchOverlapping(IReadOnlyList<ConceptGroup> groups, IReadOnlyList<SynthesizedConcept> stored, SynthesizedConcept?[] matched, HashSet<string> claimed)
    {
        var members = stored.ToDictionary(c => c.Key, c => c.Members.Select(m => m.Record).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        var overlaps = new List<(int Group, SynthesizedConcept Concept, int Shared)>();
        for (var i = 0; i < groups.Count; i++)
        {
            if (matched[i] is not null)
            {
                continue;
            }

            foreach (var concept in stored.Where(c => !claimed.Contains(c.Key)))
            {
                var shared = groups[i].Members.Count(m => members[concept.Key].Contains(m.Record));
                if (shared > 0)
                {
                    overlaps.Add((i, concept, shared));
                }
            }
        }

        foreach (var (group, concept, _) in overlaps.OrderByDescending(o => o.Shared).ThenBy(o => o.Group).ThenBy(o => o.Concept.Key, StringComparer.Ordinal))
        {
            if (matched[group] is null && claimed.Add(concept.Key))
            {
                matched[group] = concept;
            }
        }
    }
}
