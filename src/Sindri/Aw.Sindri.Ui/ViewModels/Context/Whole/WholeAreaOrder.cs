using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>
/// The left-to-right order of the areas (whole-map spec §3.2): by the barycenter of the edges between areas, ties by key, the area of
/// what no feature claims last. Deterministic for an answer, and moves little between map versions.
/// </summary>
internal static class WholeAreaOrder
{
    private const int Passes = 4;

    /// <summary>Orders the areas.</summary>
    /// <param name="areas">The areas.</param>
    /// <param name="edges">The edges between them.</param>
    /// <returns>The keys, in order.</returns>
    internal static IReadOnlyList<string> Of(IReadOnlyList<WholeArea> areas, IReadOnlyList<WholeAreaEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(areas);
        ArgumentNullException.ThrowIfNull(edges);
        var assigned = areas.Where(a => !a.Unassigned).Select(a => a.Key).Order(NaturalOrder.Instance).ToList();
        var unassigned = areas.Where(a => a.Unassigned).Select(a => a.Key).Order(NaturalOrder.Instance).ToList();
        var neighbours = Neighbours(edges, assigned);
        for (var pass = 0; pass < Passes; pass++)
        {
            // Each area moves to the weighted mean of where its neighbours stand now (areas already moved this pass count where they went), then the pass ends with a stable sort.
            var place = assigned.Select((key, i) => (key, i)).ToDictionary(p => p.key, p => (double)p.i, StringComparer.Ordinal);
            foreach (var key in assigned.Where(k => neighbours[k].Count > 0))
            {
                place[key] = Barycenter(key, neighbours, place);
            }

            assigned = [.. assigned.OrderBy(key => place[key]).ThenBy(key => key, NaturalOrder.Instance)];
        }

        return [.. assigned, .. unassigned];
    }

    // Each assigned area's neighbours with the number of edges that join them (both directions); the unassigned area pulls on nobody.
    private static Dictionary<string, List<(string Other, int Count)>> Neighbours(IReadOnlyList<WholeAreaEdge> edges, List<string> assigned)
    {
        var known = assigned.ToHashSet(StringComparer.Ordinal);
        var result = assigned.ToDictionary(k => k, _ => new List<(string, int)>(), StringComparer.Ordinal);
        foreach (var edge in edges.Where(e => known.Contains(e.From) && known.Contains(e.To) && !string.Equals(e.From, e.To, StringComparison.Ordinal)))
        {
            result[edge.From].Add((edge.To, edge.Count));
            result[edge.To].Add((edge.From, edge.Count));
        }

        return result;
    }

    // The mean position of the neighbours, weighted by edge count.
    private static double Barycenter(string key, Dictionary<string, List<(string Other, int Count)>> neighbours, Dictionary<string, double> place)
    {
        var near = neighbours[key];
        return near.Sum(n => place[n.Other] * n.Count) / near.Sum(n => n.Count);
    }
}
