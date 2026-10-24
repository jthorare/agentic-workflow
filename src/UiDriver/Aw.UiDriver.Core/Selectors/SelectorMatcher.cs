using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Core.Selectors;

#pragma warning disable MA0182 // used by the Avalonia adapter and the CLI, which the analyzer can't see from here

/// <summary>How a selector picks elements from a tree walked in order.</summary>
internal static class SelectorMatcher
{
    /// <summary>True when <paramref name="node"/> satisfies every field the selector gives (the index is not a field of one node).</summary>
    /// <param name="selector">The selector.</param>
    /// <param name="node">The element's facts.</param>
    /// <returns>Whether it matches.</returns>
    internal static bool Matches(Selector selector, NodeFacts node) =>
        MatchesIdentity(selector, node) && MatchesText(selector, node);

    private static bool MatchesIdentity(Selector selector, NodeFacts node) =>
        Same(selector.NodeId, node.Id)
        && Same(selector.AutomationId, node.AutomationId)
        && Same(selector.Name, node.Name)
        && Same(selector.Type, node.Type);

    private static bool MatchesText(Selector selector, NodeFacts node) =>
        Same(selector.Text, node.Text)
        && (selector.TextContains is null || (node.Text?.Contains(selector.TextContains, StringComparison.Ordinal) ?? false));

    private static bool Same(string? wanted, string? actual) => wanted is null || string.Equals(wanted, actual, StringComparison.Ordinal);

    /// <summary>The nodes a selector matches, in the order given.</summary>
    /// <param name="selector">The selector.</param>
    /// <param name="nodes">The tree's nodes in tree order.</param>
    /// <returns>All matches; the index does not narrow this list.</returns>
    internal static IReadOnlyList<NodeFacts> All(Selector selector, IEnumerable<NodeFacts> nodes) => [.. nodes.Where(n => Matches(selector, n))];

    /// <summary>The one node a verb acts on: the match at the selector's index (the first by default).</summary>
    /// <param name="selector">The selector.</param>
    /// <param name="nodes">The tree's nodes in tree order.</param>
    /// <returns>The node, or null when there are fewer matches than the index asks for.</returns>
    internal static NodeFacts? Pick(Selector selector, IEnumerable<NodeFacts> nodes)
    {
        var matches = All(selector, nodes);
        var index = selector.Index ?? 0;
        return index >= 0 && index < matches.Count ? matches[index] : null;
    }
}
