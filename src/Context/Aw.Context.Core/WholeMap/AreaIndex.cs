namespace Aw.Context.Core.WholeMap;

/// <summary>Every node's area, from one map version by <see cref="MapAreas"/>.</summary>
/// <param name="ByNode">The area of each node, by node key.</param>
public sealed record AreaIndex(IReadOnlyDictionary<string, NodeArea> ByNode)
{
    /// <summary>The key of the group of nodes no feature claims.</summary>
    public const string UnassignedKey = "area:unassigned";

    /// <summary>The band concept nodes sit in: a band of their own above the areas, never an area (no area lists them and no area edge reaches them).</summary>
    public const string ConceptBandKey = "area:concepts";

    /// <summary>The unassigned group's name, as people read it.</summary>
    public const string UnassignedLabel = "Not linked to a feature yet";
}
