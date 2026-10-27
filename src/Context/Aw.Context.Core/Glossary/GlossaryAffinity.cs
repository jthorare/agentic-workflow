using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Glossary;

/// <summary>How much two features' glossary terms overlap.</summary>
public static class GlossaryAffinity
{
    /// <summary>The Jaccard overlap of the terms each feature carries, 0 to 1; 0 when either carries none.</summary>
    /// <param name="glossary">The glossary.</param>
    /// <param name="a">One feature.</param>
    /// <param name="b">The other feature.</param>
    /// <returns>The overlap.</returns>
    public static double Between(MapGlossary glossary, NodeKey a, NodeKey b)
    {
        var left = TermsOf(glossary, a);
        var right = TermsOf(glossary, b);
        var shared = left.Intersect(right, StringComparer.Ordinal).Count();
        var all = left.Count + right.Count - shared;
        return all == 0 ? 0 : (double)shared / all;
    }

    private static HashSet<string> TermsOf(MapGlossary glossary, NodeKey feature) =>
        [.. glossary.Terms.Where(t => t.Features.Contains(feature)).Select(t => t.Term)];
}
