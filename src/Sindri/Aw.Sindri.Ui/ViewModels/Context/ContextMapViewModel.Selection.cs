using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>Selection, hover and the highlight they light (F3 spec 1 §2 and §5). The page opens the inspector for a selection; this class only knows which node is chosen and what stays lit.</summary>
internal sealed partial class ContextMapViewModel
{
    private static readonly IReadOnlySet<string> _none = new HashSet<string>(StringComparer.Ordinal);
    private readonly IReadOnlyList<MapLink> _links;
    private MapHighlight? _hover;

    /// <summary>The key of the selected node, or null.</summary>
    public string? SelectedKey { get; private set; }

    /// <summary>The keys of the edges drawn in the lit colour.</summary>
    internal IReadOnlySet<string> HighlightedEdges { get; private set; } = _none;

    /// <summary>The keys of the edges drawn dimmed.</summary>
    internal IReadOnlySet<string> DimmedEdges { get; private set; } = _none;

    /// <summary>Gets or sets what the view calls when a node is chosen by the pointer (the page reads the node and opens the inspector).</summary>
    internal Action<string>? Chosen { get; set; }

    /// <summary>Selects a node.</summary>
    /// <param name="key">The node's key, or null to select none.</param>
    /// <returns>False when the map holds no such node (nothing changes).</returns>
    internal bool Select(string? key)
    {
        if (key is not null && Node(key) is null)
        {
            return false;
        }

        SelectedKey = key;
        OnChanged(nameof(SelectedKey));
        Light();
        return true;
    }

    /// <summary>The key of the node after the selected one in reading order (the first when none is selected; the last stays last).</summary>
    /// <returns>The key, or null for an empty map.</returns>
    internal string? NextKey() => Step(+1, first: true);

    /// <summary>The key of the node before the selected one (the last when none is selected; the first stays first).</summary>
    /// <returns>The key, or null for an empty map.</returns>
    internal string? PreviousKey() => Step(-1, first: false);

    /// <summary>Lights the nodes around some keys (or only those keys).</summary>
    /// <param name="keys">The hovered node, or the keys a cite names.</param>
    /// <param name="neighbours">Whether the neighbours stay lit too (a node), or only the keys (a cite).</param>
    internal void Hover(IEnumerable<string> keys, bool neighbours = true)
    {
        _hover = neighbours ? MapHighlight.Around(_links, keys) : MapHighlight.Among(_links, keys);
        Light();
    }

    /// <summary>The pointer left: the selection's highlight (if any) is lit again.</summary>
    internal void ClearHover()
    {
        _hover = null;
        Light();
    }

    /// <summary>The path from the nearest starting point to a node, in words.</summary>
    /// <param name="key">The node's key.</param>
    /// <returns>The sentence.</returns>
    internal string PathSentence(string key) =>
        MapPath.Sentence(k => Node(k)?.Label ?? k, MapPath.Find(_links, Layout.Nodes.Where(n => n.IsAnchor).Select(n => n.Key), key));

    /// <summary>The conflicts a node is part of: its own decision, the one it disagrees with, or the node both apply to.</summary>
    /// <param name="key">The node's key.</param>
    /// <returns>The conflicts, as served.</returns>
    internal IReadOnlyList<LensConflict> ConflictsFor(string key) =>
        [.. Conflicts.Where(c => string.Equals(c.Node, key, StringComparison.Ordinal) || string.Equals(c.Decision, key, StringComparison.Ordinal) || string.Equals(c.Other, key, StringComparison.Ordinal))];

    /// <summary>The link a suggestion on this node waits on: its own (a suggested file) or the first suggested edge at it.</summary>
    /// <param name="key">The node's key.</param>
    /// <returns>The link id, or null when nothing here waits for a person.</returns>
    internal string? SuggestionLink(string key) =>
        Node(key)?.Layout.LinkId
        ?? Layout.Edges.FirstOrDefault(e => e.Dashed && e.Link is not null && (string.Equals(e.From, key, StringComparison.Ordinal) || string.Equals(e.To, key, StringComparison.Ordinal)))?.Link;

    private string? Step(int by, bool first)
    {
        if (Nodes.Count == 0)
        {
            return null;
        }

        var at = SelectedKey is null ? -1 : Nodes.ToList().FindIndex(n => string.Equals(n.Key, SelectedKey, StringComparison.Ordinal));
        return at < 0 ? Nodes[first ? 0 : ^1].Key : Nodes[Math.Clamp(at + by, 0, Nodes.Count - 1)].Key;
    }

    // Hover wins over the selection while the pointer is on a node; the selection's highlight is what stays when it leaves.
    private void Light()
    {
        var lit = _hover ?? (SelectedKey is null ? null : MapHighlight.Around(_links, [SelectedKey]));
        foreach (var node in Nodes)
        {
            node.Apply(string.Equals(node.Key, SelectedKey, StringComparison.Ordinal), lit?.Dims(node.Key) == true);
        }

        foreach (var pill in Pills)
        {
            pill.Apply(lit);
        }

        HighlightedEdges = lit?.Edges ?? _none;
        DimmedEdges = lit is null ? _none : _links.Where(l => lit.DimsEdge(l.Key)).Select(l => l.Key).ToHashSet(StringComparer.Ordinal);
        OnChanged(nameof(HighlightedEdges));
        OnChanged(nameof(DimmedEdges));
    }
}
