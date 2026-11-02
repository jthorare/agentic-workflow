using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Glossary;

/// <summary>Builds a <see cref="MapGlossary"/> from feature vectors: pure and deterministic, no I/O.</summary>
public static class GlossaryBuilder
{
    /// <summary>Clusters the features by symbol embedding and ranks each cluster's terms.</summary>
    /// <param name="features">The features with their symbol vectors, in any order.</param>
    /// <param name="rules">The similarity, term count, feature cap and stoplist.</param>
    /// <returns>The glossary; the same input gives the same output.</returns>
    public static MapGlossary Build(IReadOnlyList<FeatureVectors> features, GlossaryRules rules)
    {
        var ordered = features.OrderBy(f => f.Feature.Value, StringComparer.Ordinal).ToList();
        var used = ordered.Take(rules.MaxFeatures).ToList();
        var vectored = new List<(FeatureVectors Feature, Centroid Centroid)>();
        var unvectored = new List<NodeKey>();
        foreach (var feature in used)
        {
            var unit = GlossaryCentroids.Unit(feature);
            if (unit is null)
            {
                unvectored.Add(feature.Feature);
                continue;
            }

            vectored.Add((feature, new Centroid(feature.Feature, unit)));
        }

        var groups = GlossaryClustering.Group([.. vectored.Select(v => v.Centroid)], rules.Similarity);
        var members = groups.Select(g => (IReadOnlyList<FeatureVectors>)[.. g.Select(i => vectored[i].Feature)]).ToList();
        var ranked = GlossaryTermRanking.Rank(members, rules);
        var clusters = members.Select((m, i) => new GlossaryCluster([.. m.Select(f => f.Feature)], [.. ranked[i].Select(t => t.Term)])).ToList();
        return new MapGlossary(MergeTerms(clusters, ranked), clusters, unvectored, Truncated: ordered.Count > rules.MaxFeatures);
    }

    // A term in several clusters takes the features of all of them and its highest weight.
    private static List<GlossaryTerm> MergeTerms(List<GlossaryCluster> clusters, List<List<(string Term, double Weight)>> ranked)
    {
        var weights = new Dictionary<string, double>(StringComparer.Ordinal);
        var features = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        for (var i = 0; i < clusters.Count; i++)
        {
            foreach (var (term, weight) in ranked[i])
            {
                weights[term] = Math.Max(weights.GetValueOrDefault(term), weight);
                if (!features.TryGetValue(term, out var keys))
                {
                    features[term] = keys = new SortedSet<string>(StringComparer.Ordinal);
                }

                keys.UnionWith(clusters[i].Features.Select(f => f.Value));
            }
        }

        return [.. weights
            .OrderByDescending(t => t.Value)
            .ThenBy(t => t.Key, StringComparer.Ordinal)
            .Select(t => new GlossaryTerm(t.Key, t.Value, [.. features[t.Key].Select(NodeKey.From)]))];
    }
}
