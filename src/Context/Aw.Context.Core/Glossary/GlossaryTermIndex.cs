namespace Aw.Context.Core.Glossary;

/// <summary>The terms each feature carries, indexed once so group-to-group overlap is cheap.</summary>
internal sealed class GlossaryTermIndex
{
    private readonly Dictionary<string, HashSet<string>> _termsOf = new(StringComparer.Ordinal);

    internal GlossaryTermIndex(MapGlossary glossary)
    {
        foreach (var term in glossary.Terms)
        {
            foreach (var feature in term.Features)
            {
                if (!_termsOf.TryGetValue(feature.Value, out var terms))
                {
                    _termsOf[feature.Value] = terms = new HashSet<string>(StringComparer.Ordinal);
                }

                _ = terms.Add(term.Term);
            }
        }
    }

    /// <summary>The Jaccard overlap of the terms the two groups' features carry, 0 to 1; 0 when either carries none.</summary>
    internal double Between(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var left = Union(a);
        var right = Union(b);
        var shared = left.Count(right.Contains);
        var all = left.Count + right.Count - shared;
        return all == 0 || left.Count == 0 || right.Count == 0 ? 0 : (double)shared / all;
    }

    private HashSet<string> Union(IReadOnlyList<string> features)
    {
        var union = new HashSet<string>(StringComparer.Ordinal);
        foreach (var feature in features)
        {
            if (_termsOf.TryGetValue(feature, out var terms))
            {
                union.UnionWith(terms);
            }
        }

        return union;
    }
}
