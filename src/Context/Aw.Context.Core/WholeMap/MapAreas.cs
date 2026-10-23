using Aw.Context.Core.Graph;

namespace Aw.Context.Core.WholeMap;

/// <summary>Gives every node of a map one area by fixed rules over the edges it already has (Part D, §2.2). Pure: the same graph always gives the same areas, whatever the order of its rows.</summary>
public static class MapAreas
{
    /// <summary>The areas of <paramref name="graph"/>.</summary>
    /// <param name="graph">One map version's graph.</param>
    /// <returns>Each node's area.</returns>
    public static AreaIndex Assign(GraphSnapshot graph) => Assign(graph, WholeMapRules.Default.AreaCap);

    /// <summary>The areas of <paramref name="graph"/>, made deeper when an area would hold more than <paramref name="areaCap"/> nodes.</summary>
    /// <param name="graph">One map version's graph.</param>
    /// <param name="areaCap">The most nodes one area may hold before its feature is replaced by its children.</param>
    /// <returns>Each node's area.</returns>
    public static AreaIndex Assign(GraphSnapshot graph, int areaCap)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return new AreaAssigner(graph, areaCap).Run();
    }
}
