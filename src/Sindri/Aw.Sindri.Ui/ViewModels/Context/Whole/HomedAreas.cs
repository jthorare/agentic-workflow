using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The feature tiles of a placed map. The layout homes a node whose area the answer does not list (a concept node sits in area:concepts) in the area of what no feature claims, so that area has no entry in the answer: its tile is made out of the nodes homed there.</summary>
internal static class HomedAreas
{
    /// <summary>One tile per area the geometry names, in its order.</summary>
    /// <param name="areas">The areas the answer lists.</param>
    /// <param name="nodes">The nodes drawn.</param>
    /// <param name="order">The geometry's area keys, in order.</param>
    /// <param name="open">What a tile does when it is opened.</param>
    /// <returns>The tiles.</returns>
    internal static IReadOnlyList<WholeAreaTileViewModel> Tiles(IEnumerable<WholeArea> areas, IEnumerable<WholeNode> nodes, IReadOnlyList<string> order, Action<string> open)
    {
        ArgumentNullException.ThrowIfNull(areas);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(open);
        var byKey = areas.ToDictionary(a => a.Key, StringComparer.Ordinal);
        var homeless = nodes.Where(n => !byKey.ContainsKey(n.Area)).ToList();
        return [.. order.Select((key, i) => new WholeAreaTileViewModel(byKey.TryGetValue(key, out var area) ? area : Homed(key, homeless), i, open))];
    }

    private static WholeArea Homed(string key, List<WholeNode> homeless) =>
        new(key, string.Empty, "trusted", homeless.Count, homeless.GroupBy(n => n.Kind, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal), Unassigned: true);
}
