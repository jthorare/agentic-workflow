namespace Aw.Context.Core.WholeMap;

/// <summary>Where one asked-for key sits.</summary>
/// <param name="Key">The key as asked.</param>
/// <param name="Area">The node's area, or null when the map has no such node.</param>
public sealed record WholeMapLocated(string Key, string? Area)
{
    /// <summary>The keys of the concepts holding the node (a member can be in several; 'concept:other' for a member no concept takes), empty for a node that is not a feature, record or decision.</summary>
    public IReadOnlyList<string> Concept { get; init; } = [];
}
