namespace Aw.Context.Core.Ranking;

/// <summary>Stage 5 of the query (spec §8.2): re-scores the fused items with the features. Pure.</summary>
public static class FeatureRanker
{
    /// <summary>The weighted mean of the features of one record, 0 to 1; 0 when every weight is 0.</summary>
    /// <param name="signals">What the map says about the record.</param>
    /// <param name="weights">The weights.</param>
    /// <param name="now">The query's time.</param>
    /// <returns>The mean.</returns>
    public static double Boost(RecordSignals signals, FeatureWeights weights, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(weights);
        var total = weights.CascadeDistance + weights.EdgeType + weights.Provenance + weights.Supersession + weights.Recency;
        if (total <= 0)
        {
            return 0;
        }

        var sum = (weights.CascadeDistance * RankFeatures.Cascade(signals.CascadeDistance))
            + (weights.EdgeType * RankFeatures.Edge(signals.Edge))
            + (weights.Provenance * RankFeatures.Source(signals.Provenance))
            + (weights.Supersession * RankFeatures.Standing(signals.Superseded))
            + (weights.Recency * RankFeatures.Recency(signals.Linked, now));
        return sum / total;
    }

    /// <summary>Multiplies each item's reciprocal-rank-fusion score by 1 + its <see cref="Boost"/> (so features can at most double a score and never reorder items by more than the fusion's own spread allows), and sorts again.</summary>
    /// <param name="items">The fused items, best first.</param>
    /// <param name="signals">The signals by the item's canonical key; an item with none counts as <see cref="RecordSignals.None"/>.</param>
    /// <param name="weights">The weights.</param>
    /// <param name="now">The query's time.</param>
    /// <returns>The items, best score first (ties keep the fusion's order).</returns>
    public static IReadOnlyList<FusedItem> Apply(IReadOnlyList<FusedItem> items, IReadOnlyDictionary<string, RecordSignals> signals, FeatureWeights weights, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(signals);
        return [.. items.Select(i => i with { Score = i.Score * (1 + Boost(signals.GetValueOrDefault(i.Key.Value) ?? RecordSignals.None, weights, now)) }).OrderByDescending(i => i.Score)];   // OrderBy is stable: ties keep the fusion's order
    }
}
