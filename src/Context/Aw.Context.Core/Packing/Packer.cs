namespace Aw.Context.Core.Packing;

/// <summary>
/// Packs items into the budget (spec §8.3): sections in fill order (graph, affects, decisions, code, records); sections
/// with no candidates are skipped and get no share; the rest split the budget evenly, the remainder goes to the first
/// section's cap, and unused cap rolls forward; within a section, greedily in the given order (best first), each item
/// whole or not at all; an item that doesn't fit is listed by its marker once. Conflicts and degraded sources are
/// outside the budget and never pass through here.
/// </summary>
public static class Packer
{
    /// <summary>Packs <paramref name="candidates"/> into <paramref name="budget"/> tokens.</summary>
    /// <param name="candidates">The items, best first within each section.</param>
    /// <param name="budget">The token budget.</param>
    /// <returns>The sections, the tokens used and whether anything was cut to a reference.</returns>
    public static Packed Pack(IReadOnlyList<PackCandidate> candidates, int budget)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var active = ActiveSections(candidates);
        var (share, remainder) = Split(budget, active.Count);
        var used = 0;
        var truncated = false;
        var sections = new List<Results.ResultSection>(active.Count);
        var rollover = 0;
        for (var index = 0; index < active.Count; index++)
        {
            var cap = share + rollover + (index == 0 ? remainder : 0);
            var filled = Fill(active[index], cap);
            used += filled.Spent;
            rollover = cap - filled.Spent;
            truncated |= filled.Truncated;
            if (filled.Section is { } section)
            {
                sections.Add(section);
            }
        }

        return new Packed(sections, used, truncated);
    }

    private static List<SectionCandidates> ActiveSections(IReadOnlyList<PackCandidate> candidates)
    {
        var byName = new Dictionary<string, List<PackCandidate>>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (!byName.TryGetValue(candidate.Section, out var list))
            {
                list = [];
                byName.Add(candidate.Section, list);
            }

            list.Add(candidate);
        }

        var active = new List<SectionCandidates>();
        foreach (var name in Results.ResultSection.FillOrder)
        {
            if (byName.TryGetValue(name, out var list))
            {
                active.Add(new SectionCandidates(name, list));
            }
        }

        return active;
    }

    private static (int Share, int Remainder) Split(int budget, int count) =>
        count == 0 ? (0, 0) : (budget / count, budget % count);

    private static FillResult Fill(SectionCandidates section, int cap)
    {
        var items = new List<Results.ResultItem>();
        var more = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var spent = 0;
        var truncated = false;
        foreach (var candidate in section.Candidates)
        {
            if (spent + candidate.Item.Tokens <= cap)
            {
                items.Add(candidate.Item);
                spent += candidate.Item.Tokens;
                continue;
            }

            truncated = true;
            if (seen.Add(candidate.Marker))
            {
                more.Add(candidate.Marker);
            }
        }

        var packed = items.Count > 0 || more.Count > 0 ? new Results.ResultSection(section.Name, items, more) : null;
        return new FillResult(packed, spent, truncated);
    }

    private sealed record SectionCandidates(string Name, IReadOnlyList<PackCandidate> Candidates);

    private sealed record FillResult(Results.ResultSection? Section, int Spent, bool Truncated);
}
