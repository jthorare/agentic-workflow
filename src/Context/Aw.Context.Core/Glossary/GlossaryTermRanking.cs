namespace Aw.Context.Core.Glossary;

/// <summary>Ranks the words of each cluster by TF-IDF across clusters.</summary>
internal static class GlossaryTermRanking
{
    // One ranked list per cluster, best first, at most 'rules.TermsPerCluster' long, with the weight of each word.
    internal static List<List<(string Term, double Weight)>> Rank(IReadOnlyList<IReadOnlyList<FeatureVectors>> clusters, GlossaryRules rules)
    {
        var counts = clusters.Select(c => Count(c, rules.Stop)).ToList();
        var documentFrequency = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var term in counts.SelectMany(c => c.Keys))
        {
            documentFrequency[term] = documentFrequency.GetValueOrDefault(term) + 1;
        }

        return [.. counts.Select(c => Top(c, documentFrequency, counts.Count, rules.TermsPerCluster))];
    }

    private static Dictionary<string, int> Count(IReadOnlyList<FeatureVectors> cluster, IReadOnlySet<string> stop)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var texts = cluster.SelectMany(f => f.Symbols.Select(s => s.Name).Append(f.Label));
        foreach (var word in texts.SelectMany(GlossaryWords.Split).Where(w => !stop.Contains(w)))
        {
            counts[word] = counts.GetValueOrDefault(word) + 1;
        }

        return counts;
    }

    // A word in every cluster scores 0; with a single cluster there is nothing to contrast with, so every word scores by frequency alone.
    private static List<(string Term, double Weight)> Top(Dictionary<string, int> counts, Dictionary<string, int> documentFrequency, int clusterCount, int take)
    {
        var total = counts.Values.Sum();
        return [.. counts
            .Select(c => (Term: c.Key, Weight: (double)c.Value / total * Idf(clusterCount, documentFrequency[c.Key])))
            .Where(c => c.Weight > 0)
            .OrderByDescending(c => c.Weight)
            .ThenBy(c => c.Term, StringComparer.Ordinal)
            .Take(take)];
    }

    private static double Idf(int clusterCount, int clustersWithTerm) =>
        clusterCount == 1 ? 1.0 : Math.Log((double)clusterCount / clustersWithTerm);
}
