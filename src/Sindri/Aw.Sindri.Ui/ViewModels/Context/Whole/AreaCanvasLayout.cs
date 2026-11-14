using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>
/// Places an opened area of at most <see cref="WholeDrillIn.CanvasNodeCap"/> nodes (#3674, scope spec 6.1): Entry points on the left, Core in the middle as
/// Requests | Services and rules | Responses, Data and effects on the right. Pure: the same nodes and width give the same numbers.
/// </summary>
internal static class AreaCanvasLayout
{
    /// <summary>The distance from one row's top to the next.</summary>
    internal const double RowPitch = 44;

    /// <summary>A node's height.</summary>
    internal const double NodeHeight = 28;

    /// <summary>The height of the headings over the first row.</summary>
    internal const double HeadingHeight = 52;

    /// <summary>The space around the world and between bands.</summary>
    internal const double Gap = 12;

    /// <summary>The width of an empty band: its heading and "none in this area".</summary>
    internal const double EmptyWidth = 128;

    private const double MinWidth = 140;
    private const double MaxWidth = 340;
    private const double Margin = 16;
    private const double CharWidth = 7.2;
    private const double Chrome = 44;   // the kind mark and the padding either side of the words

    /// <summary>Places an area's nodes and the links among them.</summary>
    /// <param name="nodes">The nodes of the page.</param>
    /// <param name="edges">The links; one whose end is not a node here is left out.</param>
    /// <param name="width">The window width in px: from 1390 the three Core groups sit side by side, below that they stack.</param>
    /// <returns>Where everything sits.</returns>
    internal static WholeMapGeometry Place(IReadOnlyList<WholeNode> nodes, IReadOnlyList<WholeEdge> edges, double width)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);
        var entry = nodes.Where(n => WholeDrillIn.BandOf(n.Kind) == AreaBand.Entry).ToList();
        var data = nodes.Where(n => WholeDrillIn.BandOf(n.Kind) == AreaBand.Data).ToList();
        var core = nodes.Where(n => WholeDrillIn.BandOf(n.Kind) == AreaBand.Core).ToList();
        var groups = Enum.GetValues<CoreGroup>().Select(g => (Group: g, Members: core.Where(n => WholeDrillIn.CoreGroupOf(n.Label) == g).OrderBy(n => n.Label, StringComparer.Ordinal).ToList())).ToList();
        var sideBySide = WholeDrillIn.CoreColumns(width) >= 2;

        var placed = new List<PlacedNode>(nodes.Count);
        var headings = new List<PlacedAreaBand>();
        var x = Margin;
        var bottom = HeadingHeight;

        var sink = new Drawn(placed, headings);
        (x, bottom) = Outer(new OuterBand(entry, Labels.ContextAreaEntry, Labels.ContextAreaEntryWhat), x, bottom, sink);

        var coreLeft = x;
        var coreRight = x;
        var coreBottom = HeadingHeight;
        var stackTop = HeadingHeight;
        foreach (var (group, members) in groups.Where(g => g.Members.Count > 0 || sideBySide))
        {
            var column = WidthOf(members);
            var left = sideBySide ? coreRight : coreLeft;
            var top = sideBySide || stackTop <= HeadingHeight ? HeadingHeight : stackTop + 28;   // stacked: room for the next group's heading
            headings.Add(new PlacedAreaBand(Labels.ContextAreaGroup(group), string.Empty, members.Count, new WholeRect(left, top - 24, column, 20), Sub: true));
            var next = Column(members, left, top, column, placed);
            coreBottom = Math.Max(coreBottom, next);
            stackTop = next;
            coreRight = sideBySide ? left + column + Gap : Math.Max(coreRight, left + column + Gap);
        }

        var coreWidth = Math.Max(coreRight - coreLeft - Gap, MinWidth);
        headings.Add(new PlacedAreaBand(Labels.ContextAreaCore, Labels.ContextAreaCoreWhat, core.Count, new WholeRect(coreLeft, 0, coreWidth, 20), Sub: false));
        x = coreLeft + coreWidth + Gap;

        (x, bottom) = Outer(new OuterBand(data, Labels.ContextAreaData, Labels.ContextAreaDataWhat), x, Math.Max(bottom, coreBottom), sink);
        var world = new WholeRect(0, 0, x - Gap + Margin, Math.Max(bottom, coreBottom) + Margin);
        return new WholeMapGeometry(world, ByArea: false, [], [], placed, [], Links(edges, placed), [], [], headings);
    }

    // An outer band (Entry, Data): one column as wide as its words, or the thin empty one.
    private static (double Right, double Bottom) Outer(OuterBand band, double x, double bottom, Drawn sink)
    {
        var members = band.Members;
        var width = members.Count == 0 ? EmptyWidth : WidthOf(members);
        sink.Headings.Add(new PlacedAreaBand(band.Title, band.Caption, members.Count, new WholeRect(x, 0, width, 20), Sub: false));
        var next = Column([.. members.OrderBy(n => n.Label, StringComparer.Ordinal)], x, HeadingHeight, width, sink.Nodes);
        return (x + width + Gap, Math.Max(bottom, next));
    }

    private static double Column(IReadOnlyList<WholeNode> members, double left, double top, double width, List<PlacedNode> placed)
    {
        var y = top;
        foreach (var node in members)
        {
            placed.Add(new PlacedNode(node.Key, node.Label, node.Kind, 0, new WholeRect(left, y, width, NodeHeight), node.Confirmed));
            y += RowPitch;
        }

        return y;
    }

    private static double WidthOf(List<WholeNode> members) =>
        members.Count == 0 ? MinWidth : Math.Clamp((members.Max(n => n.Label.Length) * CharWidth) + Chrome, MinWidth, MaxWidth);

    // A link leaves a node's side when the two sit in different columns, else its top or bottom edge.
    private static List<PlacedEdge> Links(IReadOnlyList<WholeEdge> edges, IReadOnlyList<PlacedNode> placed)
    {
        var at = placed.ToDictionary(p => p.Key, p => p.Rect, StringComparer.Ordinal);
        List<PlacedEdge> links = [];
        foreach (var e in edges)
        {
            if (!at.TryGetValue(e.From, out var a) || !at.TryGetValue(e.To, out var b) || string.Equals(e.From, e.To, StringComparison.Ordinal))
            {
                continue;
            }

            var sameColumn = Math.Abs(a.X - b.X) < 1;
            var (x1, y1, x2, y2) = sameColumn
                ? (a.CenterX, b.Y > a.Y ? a.Bottom : a.Y, b.CenterX, b.Y > a.Y ? b.Y : b.Bottom)
                : b.X > a.X ? (a.Right, a.CenterY, b.X, b.CenterY) : (a.X, a.CenterY, b.Right, b.CenterY);
            links.Add(new PlacedEdge(e.From, e.To, e.Type, e.Confirmed, x1, y1, x2, y2, e.Confidence));
        }

        return links;
    }

    private sealed record OuterBand(List<WholeNode> Members, string Title, string Caption);

    private sealed record Drawn(List<PlacedNode> Nodes, List<PlacedAreaBand> Headings);
}
