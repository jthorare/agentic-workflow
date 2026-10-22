using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Ranking;

/// <summary>The five ranker features, each a pure score from 0 (worst) to 1 (best) for one fused record (spec §8.2 step 5).</summary>
public static class RankFeatures
{
    /// <summary>Days after which a link's recency score halves.</summary>
    public const double RecencyHalfLifeDays = 90;

    private static readonly Dictionary<EdgeType, double> _edgeScores = new() { [EdgeType.Decides] = 1, [EdgeType.Quotes] = 0.7, [EdgeType.Mentions] = 0.4 };

    /// <summary>Cascade distance: 1 for the anchor's own scope, then 1/2, 1/3, ...; 0 for a record not linked to the answer.</summary>
    /// <param name="distance">The record's cascade distance, or null.</param>
    /// <returns>The score.</returns>
    public static double Cascade(int? distance) => distance is { } d ? 1d / (1 + Math.Max(0, d)) : 0;

    /// <summary>Edge type: a record that decides a node is worth 1, one that quotes it 0.7, one that mentions it 0.4, any other link 0.2, none 0.</summary>
    /// <param name="edge">The link's type, or null.</param>
    /// <returns>The score.</returns>
    public static double Edge(EdgeType? edge) => edge is { } type ? _edgeScores.GetValueOrDefault(type, 0.2) : 0;

    /// <summary>Provenance: human 1, deterministic 0.7, model 0.4, no link 0.</summary>
    /// <param name="provenance">Who made the link, or null.</param>
    /// <returns>The score.</returns>
    public static double Source(Provenance? provenance) => provenance switch
    {
        null => 0,
        Provenance.Human => 1,
        Provenance.Deterministic => 0.7,
        Provenance.Model => 0.4,
        _ => throw new ArgumentOutOfRangeException(nameof(provenance)),
    };

    /// <summary>Supersession: 1 for a record that stands, 0 for one a later decision replaced.</summary>
    /// <param name="superseded">Whether it was replaced.</param>
    /// <returns>The score.</returns>
    public static double Standing(bool superseded) => superseded ? 0 : 1;

    /// <summary>Recency: 1 for a link written now, halving every <see cref="RecencyHalfLifeDays"/> days; 0 when the date is unknown; a date in the future counts as now.</summary>
    /// <param name="linked">When the link was written, or null.</param>
    /// <param name="now">The query's time.</param>
    /// <returns>The score.</returns>
    public static double Recency(DateTimeOffset? linked, DateTimeOffset now) =>
        linked is { } at ? Math.Pow(0.5, Math.Max(0, (now - at).TotalDays) / RecencyHalfLifeDays) : 0;
}
