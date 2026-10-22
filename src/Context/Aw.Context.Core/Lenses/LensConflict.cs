using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>Two items that disagree, on one decided node. 'supersedes', or 'contradicts' (a checked decision pair, or a decision citing a file the map says implements something else).</summary>
/// <param name="Kind">Supersedes or contradicts.</param>
/// <param name="Node">The decided node in the own scope.</param>
/// <param name="Decision">The superseding decision.</param>
/// <param name="Other">The superseded decision (or, for 'contradicts', a citation marker).</param>
/// <param name="Summary">One line of words from keys only.</param>
/// <param name="Intent">Citation markers on the intent side (empty in S-4).</param>
/// <param name="Behavior">Citation markers on the behavior side (empty in S-4).</param>
public sealed record LensConflict(ConflictKind Kind, NodeKey Node, NodeKey Decision, string Other, string Summary, IReadOnlyList<string> Intent, IReadOnlyList<string> Behavior);
