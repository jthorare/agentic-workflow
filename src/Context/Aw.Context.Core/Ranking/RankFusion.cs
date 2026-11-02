using System.Globalization;

namespace Aw.Context.Core.Ranking;

/// <summary>
/// Fusion (spec §8.2 step 4): hits sharing a canonical key merge first, keeping each source's best rank (§5.2 dedup);
/// each source is then re-ranked by those best ranks, so a source with more sub-queries can't dominate; then
/// reciprocal rank fusion across sources, score = Σ 1 / (k + rank). Source scores never enter it, only ranks.
/// </summary>
public static class RankFusion
{
    /// <summary>The RRF constant.</summary>
    public const int K = 60;

    /// <summary>The hits ordered by their dense per-source rank (from 'rrf:&lt;source&gt;#&lt;rank&gt;'), ties by source id, so Hits[0] is the best.</summary>
    private static List<Sources.SourceHit> BestFirst(List<Sources.SourceHit> hits, List<string> why) =>
        [.. hits.Select((h, i) => (Hit: h, Rank: int.Parse(why[i].AsSpan(why[i].LastIndexOf('#') + 1), CultureInfo.InvariantCulture)))
            .OrderBy(x => x.Rank).ThenBy(x => x.Hit.Ref.Source.Value, StringComparer.Ordinal).Select(x => x.Hit)];

    /// <summary>Fuses <paramref name="answers"/>.</summary>
    /// <param name="answers">Each source's hits.</param>
    /// <returns>One item per canonical key, best score first (ties by key, ordinal).</returns>
    public static IReadOnlyList<FusedItem> Fuse(IReadOnlyList<SourceAnswer> answers) => Fuse(answers, []);

    /// <summary>Fuses <paramref name="answers"/> and adds the key-only <paramref name="legs"/>: the r-th key of a leg adds 1/(k + r) to its item and 'rrf:&lt;leg&gt;#r' to its 'why'.</summary>
    /// <param name="answers">Each source's hits.</param>
    /// <param name="legs">Key-only legs; a key no source returned is ignored (a leg never adds an item) and doesn't count toward the rank.</param>
    /// <returns>One item per canonical key, best score first (ties by key, ordinal).</returns>
    public static IReadOnlyList<FusedItem> Fuse(IReadOnlyList<SourceAnswer> answers, IReadOnlyList<RankedLeg> legs)
    {
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(legs);
        var items = new Dictionary<string, (double Score, List<Sources.SourceHit> Hits, List<string> Why)>(StringComparer.Ordinal);
        foreach (var answer in answers)
        {
            var best = answer.Hits
                .GroupBy(h => h.Ref.Canonical.Value, StringComparer.Ordinal)
                .Select(g => g.MinBy(h => h.Rank)!)   // why: a group is never empty
                .OrderBy(h => h.Rank)
                .ThenBy(h => h.Ref.Canonical.Value, StringComparer.Ordinal);
            var rank = 0;
            foreach (var hit in best)
            {
                rank++;
                var key = hit.Ref.Canonical.Value;
                var entry = items.TryGetValue(key, out var found) ? found : (0d, [], []);
                entry.Hits.Add(hit);
                entry.Why.Add(string.Create(CultureInfo.InvariantCulture, $"rrf:{answer.Source.Value}#{rank}"));
                items[key] = (entry.Item1 + (1d / (K + rank)), entry.Hits, entry.Why);
            }
        }

        foreach (var leg in legs)
        {
            var rank = 0;
            foreach (var key in leg.Keys.Select(k => k.Value).Distinct(StringComparer.Ordinal).Where(items.ContainsKey))
            {
                rank++;
                var (score, hits, why) = items[key];
                why.Add(string.Create(CultureInfo.InvariantCulture, $"rrf:{leg.Source.Value}#{rank}"));
                items[key] = (score + (1d / (K + rank)), hits, why);
            }
        }

        var all = answers.SelectMany(a => a.Hits).ToLookup(h => h.Ref.Canonical.Value, StringComparer.Ordinal);
        return [.. items
            .Select(i => Item(i.Value.Hits[0].Ref.Canonical, i.Value.Score, BestFirst(i.Value.Hits, i.Value.Why), i.Value.Why, all[i.Key]))
            .OrderByDescending(i => i.Score)
            .ThenBy(i => i.Key.Value, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The fused item, with the aliases of every hit of its record (sub-queries and chunks too, not only each source's best
    /// hit): source by source in best-first order, each source's hits by rank, each alias once.
    /// </summary>
    private static FusedItem Item(Sources.RecordKey key, double score, List<Sources.SourceHit> best, List<string> why, IEnumerable<Sources.SourceHit> all)
    {
        var hits = all.ToList();
        var aliases = best
            .SelectMany(b => hits.Where(h => h.Ref.Source == b.Ref.Source).OrderBy(h => h.Rank).SelectMany(h => h.Aliases))
            .Distinct(StringComparer.Ordinal);
        return new FusedItem(key, score, best, why) { Aliases = [.. aliases] };
    }
}
