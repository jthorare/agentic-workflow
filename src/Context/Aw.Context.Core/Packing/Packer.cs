namespace Aw.Context.Core.Packing;

/// <summary>
/// Groups the ranked items into sections (spec §8.3): sections in fill order (graph, affects, decisions, code, records),
/// sections with no candidates skipped, and every item kept, whole and in the given order (best first). There is no token
/// budget: the answer holds the whole ranked result set, and a caller pages it. Conflicts and degraded sources never pass
/// through here.
/// </summary>
public static class Packer
{
    /// <summary>Groups <paramref name="candidates"/> into sections.</summary>
    /// <param name="candidates">The items, best first within each section.</param>
    /// <returns>The sections and the tokens their items take; nothing is dropped.</returns>
    public static Packed Pack(IReadOnlyList<PackCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var byName = new Dictionary<string, List<Results.ResultItem>>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (!byName.TryGetValue(candidate.Section, out var list))
            {
                list = [];
                byName.Add(candidate.Section, list);
            }

            list.Add(candidate.Item);
        }

        var sections = new List<Results.ResultSection>();
        foreach (var name in Results.ResultSection.FillOrder)
        {
            if (byName.TryGetValue(name, out var items))
            {
                sections.Add(new Results.ResultSection(name, items));
            }
        }

        return new Packed(sections, sections.Sum(s => s.Items.Sum(i => i.Tokens)));
    }
}
