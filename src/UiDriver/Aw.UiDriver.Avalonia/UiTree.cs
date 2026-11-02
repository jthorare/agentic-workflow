using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel;
using Aw.UiDriver.Core;
using Aw.UiDriver.Core.Protocol;
using Aw.UiDriver.Core.Selectors;

namespace Aw.UiDriver.Avalonia;

/// <summary>
/// The window's visual tree as the driver sees it: every element with a stable id (child positions from the window, '0/2/1'), its facts for selectors and
/// its info for answers. Built on the UI thread and used there; read through the property system, never reflection. The content of each open popup that
/// draws in its own popup window hangs under the window as one more child, with ids 'p3', 'p3/0' (the popup's open number, then child positions), so
/// a menu or flyout is reached like anything else.
/// </summary>
internal sealed class UiTree
{
    internal const string RootId = "0";

    private readonly List<(NodeFacts Facts, Visual Visual)> _nodes = [];
    private readonly IReadOnlyList<(string Id, Visual Visual)> _popups = [.. OpenPopups.Current().Select(static o => (PopupId(o.Sequence), o.Content))];

    internal UiTree(Window root)
    {
        Root = root;
        Walk(root, RootId);
    }

    /// <summary>Every element in tree order, window first.</summary>
    internal IReadOnlyList<(NodeFacts Facts, Visual Visual)> Nodes => _nodes;

    internal Window Root { get; }

    /// <summary>The element a selector means: the match at its index.</summary>
    /// <param name="selector">The selector; it must name something.</param>
    /// <returns>The element, AW-UIDRIVER-001 for an empty selector or AW-UIDRIVER-002 when nothing matches.</returns>
    internal Result<(NodeFacts Facts, Visual Visual)> Resolve(Selector selector)
    {
        if (selector.IsEmpty)
        {
            return UiDriverErrors.Invalid.With("a selector must name something (nodeId, automationId, name, type, text or textContains)");
        }

        return Find(selector) is { } found ? found : UiDriverErrors.NoMatch.With($"no element matches {selector.Describe()}");
    }

    /// <summary>The element an act verb means: the match at the selector's index among the visible matches only.</summary>
    /// <param name="selector">The selector; it must name something.</param>
    /// <returns>The element, AW-UIDRIVER-001 for an empty selector, AW-UIDRIVER-002 when nothing visible matches at the index, or AW-UIDRIVER-003 when every match is hidden.</returns>
    internal Result<(NodeFacts Facts, Visual Visual)> ResolveVisible(Selector selector)
    {
        if (selector.IsEmpty)
        {
            return UiDriverErrors.Invalid.With("a selector must name something (nodeId, automationId, name, type, text or textContains)");
        }

        var facts = _nodes.Select(static n => n.Facts);
        if (SelectorMatcher.PickVisible(selector, facts) is { } found)
        {
            return _nodes.First(n => ReferenceEquals(n.Facts, found));
        }

        var matches = SelectorMatcher.All(selector, facts);
        if (matches.Count == 0)
        {
            return UiDriverErrors.NoMatch.With($"no element matches {selector.Describe()}");
        }

        return matches.All(static m => !m.Visible)
            ? UiDriverErrors.NotActionable.With($"{selector.Describe()} matches only hidden elements")
            : UiDriverErrors.NoMatch.With($"no visible element matches {selector.Describe()}");
    }

    /// <summary>The element a selector means, or null; an empty selector means the first (the window).</summary>
    /// <param name="selector">The selector.</param>
    /// <returns>The element, or null.</returns>
    internal (NodeFacts Facts, Visual Visual)? Find(Selector selector)
    {
        var facts = SelectorMatcher.Pick(selector, _nodes.Select(static n => n.Facts));
        return facts is null ? null : _nodes.First(n => ReferenceEquals(n.Facts, facts));
    }

    /// <summary>The element as the driver reports it, with its children to <paramref name="depth"/> levels.</summary>
    /// <param name="id">Its node id.</param>
    /// <param name="visual">The element.</param>
    /// <param name="depth">How many levels of children to include.</param>
    /// <returns>The info.</returns>
    internal NodeInfo InfoOf(string id, Visual visual, int depth)
    {
        var facts = FactsOf(id, visual);
        var origin = visual.TranslatePoint(default, Root) ?? OriginInOwnRoot(visual);
        List<NodeInfo> children = depth <= 0 ? [] : [.. ChildrenOf(id, visual).Select(c => InfoOf(c.Id, c.Visual, depth - 1))];
        return new NodeInfo(id, facts.Type, facts.AutomationId, facts.Name, facts.Text, facts.Visible, facts.Enabled, origin.X, origin.Y, visual.Bounds.Width, visual.Bounds.Height, children);
    }

    /// <summary>The info of an element already in the tree, without children.</summary>
    /// <param name="node">The element.</param>
    /// <returns>The info.</returns>
    internal NodeInfo InfoOf((NodeFacts Facts, Visual Visual) node) => InfoOf(node.Facts.Id, node.Visual, 0);

    private IEnumerable<(string Id, Visual Visual)> ChildrenOf(string id, Visual visual)
    {
        var children = visual.GetVisualChildren().Select((child, index) => (id + "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), child));
        return string.Equals(id, RootId, StringComparison.Ordinal) ? children.Concat(_popups) : children;
    }

    private static string PopupId(int sequence) => "p" + sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // An element in a popup window is outside the main window, so its position is given in its own popup window.
    private static Point OriginInOwnRoot(Visual visual) => TopLevel.GetTopLevel(visual) is Visual root ? visual.TranslatePoint(default, root) ?? default : default;

    private void Walk(Visual visual, string id)
    {
        _nodes.Add((FactsOf(id, visual), visual));
        foreach (var (childId, child) in ChildrenOf(id, visual))
        {
            Walk(child, childId);
        }
    }

    private static NodeFacts FactsOf(string id, Visual visual)
    {
        var control = visual as Control;
        var automationId = control is null ? null : NullIfEmpty(AutomationProperties.GetAutomationId(control));
        var name = control is null ? null : NullIfEmpty(AutomationProperties.GetName(control)) ?? NullIfEmpty(control.Name);
        var enabled = visual is not InputElement input || input.IsEffectivelyEnabled;
        return new NodeFacts(id, visual.GetType().Name, automationId, name, TextOf(visual), visual.IsEffectivelyVisible, enabled);
    }

    // What the element itself shows. A button's label that is a child TextBlock is that child's text, so each text is reported once.
    private static string? TextOf(Visual visual) => visual switch
    {
        TextBlock block => NullIfEmpty(block.Text),
        TextBox box => NullIfEmpty(box.Text),
        ContentControl { Content: string content } => NullIfEmpty(content),
        HeaderedContentControl { Header: string header } => NullIfEmpty(header),
        HeaderedSelectingItemsControl { Header: string header } => NullIfEmpty(header),
        _ => null,
    };

    private static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;
}
