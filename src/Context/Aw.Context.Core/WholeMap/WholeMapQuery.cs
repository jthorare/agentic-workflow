using Aw.Context.Core.Map;

namespace Aw.Context.Core.WholeMap;

/// <summary>The whole-map question: how much to send, which keys to place, which map to read, and whose repo.</summary>
/// <param name="Detail">'auto', 'nodes' or 'areas' as a transport sent it, or null for auto (anything else is SND-CONTEXT-006).</param>
/// <param name="Locate">Node keys whose area the answer names (up to <see cref="WholeMapRules.MaxLocate"/>).</param>
/// <param name="Read">Which map: the current version, the draft or a retained version.</param>
/// <param name="Repo">A human caller's repo when the profile has more than one, else null (a session is always its own repo).</param>
/// <param name="Area">The key of one area to open on its own (its nodes and edges in full, up to the area cap), or null.</param>
public sealed record WholeMapQuery(string? Detail, IReadOnlyList<string> Locate, MapRead Read, string? Repo = null, string? Area = null)
{
    /// <summary>The key of one concept to open on its own, like <see cref="Area"/> but scoped to the concept's members and what lies two levels below them; together with an area, the area's nodes that scope holds.</summary>
    public string? Concept { get; init; }
}
