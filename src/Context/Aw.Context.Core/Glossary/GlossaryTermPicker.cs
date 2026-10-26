namespace Aw.Context.Core.Glossary;

/// <summary>Picks the glossary terms that characterize a set of features.</summary>
internal static class GlossaryTermPicker
{
    /// <summary>The terms the most of the features carry, then the heaviest, then by name.</summary>
    internal static List<string> Top(MapGlossary? glossary, IReadOnlyList<string> features, int take)
    {
        if (glossary is null || features.Count == 0)
        {
            return [];
        }

        var wanted = features.ToHashSet(StringComparer.Ordinal);
        return [.. glossary.Terms
            .Select(t => (t.Term, t.Weight, Hits: t.Features.Count(f => wanted.Contains(f.Value))))
            .Where(t => t.Hits > 0)
            .OrderByDescending(t => t.Hits)
            .ThenByDescending(t => t.Weight)
            .ThenBy(t => t.Term, StringComparer.Ordinal)
            .Take(take)
            .Select(t => t.Term)];
    }
}
