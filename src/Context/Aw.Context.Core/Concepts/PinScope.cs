using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Kernel;

namespace Aw.Context.Core.Concepts;

/// <summary>One pin as a query sees it: what is inside it (concept pins spec §3.1). The map's own grouping decides, so a file is in one pin.</summary>
public sealed class PinScope
{
    private readonly PinSet _set;

    internal PinScope(PinSet set, ConceptId pin)
    {
        (_set, Pin) = (set, pin);
    }

    /// <summary>The pin.</summary>
    public ConceptId Pin { get; }

    /// <summary>The pin <paramref name="pin"/> names, as the caller may see it.</summary>
    /// <param name="mapped">The repos the map is built from; the ids are made over these, so 'billing' is the pin the map shows.</param>
    /// <param name="visible">The caller's repos; a pin only they don't see answers like a typo.</param>
    /// <param name="pin">A pin id or 'concept:&lt;id&gt;'.</param>
    /// <param name="graph">The map's whole stored graph (before it is cut to the caller's scope).</param>
    /// <param name="mapTag">How answers name the map ('v41').</param>
    /// <returns>The scope, or SND-CONTEXT-102 (no such pin; at most 5 visible ones named), 103 (no pins) or 104 (the map has no concept node for it yet).</returns>
    public static Result<PinScope> Of(IReadOnlyList<ContextRepo> mapped, IReadOnlyList<ContextRepo> visible, string pin, GraphSnapshot graph, string mapTag) =>
        PinSet.Of(mapped, visible, graph).Scope(pin, mapTag);

    /// <summary>Whether a repo file is in this pin: the first pin of that repo whose paths match it holds it.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="repoRelativePath">The path in the repo.</param>
    /// <returns>True when this pin holds it.</returns>
    public bool HoldsPath(ContextRepoName repo, string repoRelativePath) => string.Equals(_set.PathPin(repo, repoRelativePath), Pin.Value, StringComparison.Ordinal);

    /// <summary>Whether a node is in this pin: its concept, a member feature, a code file through its home feature, or a record or decision that mentions either.</summary>
    /// <param name="key">The node.</param>
    /// <returns>True when this pin holds it.</returns>
    public bool HoldsNode(NodeKey key) => _set.PinOf(key) is { } held && string.Equals(held.Value, Pin.Value, StringComparison.Ordinal);

    /// <summary>The pin an anchor sits in.</summary>
    /// <param name="anchor">The anchor.</param>
    /// <param name="mapped">The repos the map is built from.</param>
    /// <param name="graph">The map's graph.</param>
    /// <returns>The pin, or null when the anchor is in none.</returns>
    public static ConceptId? PinOf(NodeKey anchor, IReadOnlyList<ContextRepo> mapped, GraphSnapshot graph) => PinSet.Of(mapped, mapped, graph).PinOf(anchor);
}
