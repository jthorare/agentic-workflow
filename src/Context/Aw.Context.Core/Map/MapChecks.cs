using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Map;

/// <summary>
/// The two structure measures a map build runs on its joined graph before it writes a draft (map structure spec §4): the share of leaf home features
/// (a feature that holds files) with at least the configured minimum of files that link to no other feature and no table, and the share over
/// <see cref="SizeCap"/> files. Deterministic edges only. "At the limit" passes. The isolated degraded limit is measured (issue 1365); the other limits are the spec's proposals, which the measured maps sat well inside.
/// </summary>
public static class MapChecks
{
    /// <summary>The most files a leaf home feature may hold before it counts as over the cap.</summary>
    public const int SizeCap = Extract.HomeFeatures.SizeCap;

    private const int Listed = 5;

    /// <summary>Measures a built graph against the default limits.</summary>
    /// <param name="graph">The joined graph a build is about to save.</param>
    /// <returns>The measures.</returns>
    public static MapChecksReport Measure(GraphSnapshot graph) => Measure(graph, new MapCheckLimits());

    /// <summary>Measures a built graph.</summary>
    /// <param name="graph">The joined graph a build is about to save.</param>
    /// <param name="limits">The profile's limits ('context.checks').</param>
    /// <returns>The measures.</returns>
    public static MapChecksReport Measure(GraphSnapshot graph, MapCheckLimits limits)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(limits);
        var (isolatedDegraded, isolatedRefuse) = (limits.IsolatedDegradedPercent * 10, limits.IsolatedRefusePercent * 10);
        var (overDegraded, overRefuse) = (limits.OverCapDegradedPercent * 10, limits.OverCapRefusePercent * 10);
        var files = graph.Edges
            .Where(static e => e is { Type: EdgeType.Implements, Provenance: Provenance.Deterministic, From.Kind: NodeKind.Code, To.Kind: NodeKind.Feature })
            .GroupBy(static e => e.To.Value, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.Select(static e => e.From.Value).Distinct(StringComparer.Ordinal).Count(), StringComparer.Ordinal);
        var linked = Linked(graph);
        var fileEdges = graph.Edges.Any(static e => e is { Provenance: Provenance.Deterministic, From.Kind: NodeKind.Code, Type: EdgeType.DependsOn or EdgeType.Reads or EdgeType.Writes });
        var measured = files.Where(f => f.Value >= limits.MinFilesIsolated).ToList();
        var noFileEdges = measured.Count >= limits.MinFeaturesIsolated && !fileEdges;
        var isolated = measured.Where(f => !linked.Contains(f.Key)).ToList();
        var over = files.Where(static f => f.Value > SizeCap).ToList();
        var isolatedMeasure = measured.Count < limits.MinFeaturesIsolated
            ? new CheckMeasure(IsolatedName, Count: 0, Total: 0, isolatedDegraded, isolatedRefuse, Skipped: $"fewer than {limits.MinFeaturesIsolated.ToString(System.Globalization.CultureInfo.InvariantCulture)} features of at least {limits.MinFilesIsolated.ToString(System.Globalization.CultureInfo.InvariantCulture)} files")
            : noFileEdges
                ? new CheckMeasure(IsolatedName, Count: 0, Total: 0, isolatedDegraded, isolatedRefuse, Skipped: "no file edges (imports and data stores not extracted)")
                : new CheckMeasure(IsolatedName, isolated.Count, measured.Count, isolatedDegraded, isolatedRefuse);
        var overMeasure = new CheckMeasure(OverCapName, over.Count, files.Count, overDegraded, overRefuse, files.Count == 0 ? "no home features" : null);
        return new MapChecksReport(isolatedMeasure, overMeasure, noFileEdges, [.. Largest(IsolatedName, isolatedMeasure.Skipped is null ? isolated : []), .. Largest(OverCapName, over)]);
    }

    /// <summary>The measure's name for the features with no link.</summary>
    internal const string IsolatedName = "isolated";

    /// <summary>The measure's name for the features over the cap.</summary>
    internal const string OverCapName = "over-cap";

    // The home features a deterministic roll-up edge touches: a depends-on between features (either end), or a reads or writes from one.
    private static HashSet<string> Linked(GraphSnapshot graph)
    {
        var linked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in graph.Edges.Where(static e => e.Provenance == Provenance.Deterministic))
        {
            switch (e)
            {
                case { Type: EdgeType.DependsOn, From.Kind: NodeKind.Feature, To.Kind: NodeKind.Feature }:
                    _ = linked.Add(e.From.Value);
                    _ = linked.Add(e.To.Value);
                    break;
                case { Type: EdgeType.Reads or EdgeType.Writes, From.Kind: NodeKind.Feature }:
                    _ = linked.Add(e.From.Value);
                    break;
                default:
                    break;
            }
        }

        return linked;
    }

    private static IEnumerable<CheckOffender> Largest(string measure, IEnumerable<KeyValuePair<string, int>> features) =>
        features.OrderByDescending(static f => f.Value).ThenBy(static f => f.Key, StringComparer.Ordinal).Take(Listed).Select(f => new CheckOffender(measure, f.Key, f.Value));
}
