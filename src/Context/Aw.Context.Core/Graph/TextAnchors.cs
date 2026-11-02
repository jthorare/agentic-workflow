namespace Aw.Context.Core.Graph;

/// <summary>What a free-text question names in the graph (spec §8.1, the 'text' shape from Phase 3).</summary>
/// <param name="Anchors">Nodes whose whole name the question uses, the most specific first.</param>
/// <param name="Suggested">Nodes that share a word with the question but aren't anchors, at most five.</param>
public sealed record TextAnchors(IReadOnlyList<NodeKey> Anchors, IReadOnlyList<NodeKey> Suggested)
{
    /// <summary>For an anchor reached through a pin's alias: the concept's key to the alias that matched (what the anchor line quotes).</summary>
    public IReadOnlyDictionary<string, string> Aliases { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
