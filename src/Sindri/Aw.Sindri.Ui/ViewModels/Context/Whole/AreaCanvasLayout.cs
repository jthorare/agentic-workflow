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
    internal const double HeadingHeight = 72;

    /// <summary>The space around the world and between bands.</summary>
    internal const double Gap = 12;

    /// <summary>The fewest rows a group's column is cut at when the layout looks for the best fit.</summary>
    internal const int MinRows = 4;

    /// <summary>The most rows a group's column holds before the group wraps into another column.</summary>
    internal const int MaxRows = 10;

    private const double NoteHeight = 22;
    private const double TitleChar = 8;
    private const double MinWidth = 140;
    private const double MaxWidth = 340;
    private const double Margin = 16;
    private const double CharWidth = 7.2;
    private const double Chrome = 44;   // the kind mark and the padding either side of the words

    /// <summary>Places an area's nodes and the links among them.</summary>
    /// <param name="nodes">The nodes of the page.</param>
    /// <param name="edges">The links; one whose end is not a node here is left out.</param>
    /// <param name="width">The window width in px; it gives the stage when <paramref name="stageWidth"/> is not known.</param>
    /// <param name="stageWidth">The width in px the map fits into, or 0 when not known.</param>
    /// <param name="stageHeight">The height in px the map fits into, or 0 when not known.</param>
    /// <returns>Where everything sits: of the Core arrangements (stacked or side by side) and row caps tried, the one that fits the stage biggest (capped at 1; a tie keeps the fewer columns).</returns>
    internal static WholeMapGeometry Place(IReadOnlyList<WholeNode> nodes, IReadOnlyList<WholeEdge> edges, double width, double stageWidth = 0, double stageHeight = 0)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);
        var (sw, sh) = stageWidth > 0 && stageHeight > 0 ? (stageWidth, stageHeight) : (Math.Max(width - 250, 400), 400);
        var best = Build(nodes, edges, sideBySide: false, MaxRows);
        var bestFit = Math.Min(1, Math.Min(sw / best.World.Width, sh / best.World.Height));
        foreach (var side in (bool[])[false, true])
        {
            for (var rows = MaxRows; rows >= MinRows; rows--)
            {
                var candidate = Build(nodes, edges, side, rows);
                var fit = Math.Min(1, Math.Min(sw / candidate.World.Width, sh / candidate.World.Height));
                if (fit > bestFit + 1e-9)
                {
                    (best, bestFit) = (candidate, fit);
                }
            }
        }

        return best;
    }

    private static WholeMapGeometry Build(IReadOnlyList<WholeNode> nodes, IReadOnlyList<WholeEdge> edges, bool sideBySide, int maxRows)
    {
        var entry = nodes.Where(n => WholeDrillIn.BandOf(n.Kind) == AreaBand.Entry).ToList();
        var data = nodes.Where(n => WholeDrillIn.BandOf(n.Kind) == AreaBand.Data).ToList();
        var core = nodes.Where(n => WholeDrillIn.BandOf(n.Kind) == AreaBand.Core).ToList();
        var groups = Enum.GetValues<CoreGroup>().Select(g => (Group: g, Members: core.Where(n => WholeDrillIn.CoreGroupOf(n.Label) == g).OrderBy(n => n.Label, StringComparer.Ordinal).ToList())).ToList();

        var placed = new List<PlacedNode>(nodes.Count);
        var headings = new List<PlacedAreaBand>();
        var x = Margin;
        var bottom = HeadingHeight;

        var sink = new Drawn(placed, headings, maxRows);
        (x, bottom) = Outer(new OuterBand(entry, Labels.ContextAreaEntry, Labels.ContextAreaEntryWhat), x, bottom, sink);

        var coreLeft = x;
        var coreRight = x;
        var coreBottom = HeadingHeight;
        var stackTop = HeadingHeight;
        foreach (var (group, members) in groups.Where(g => g.Members.Count > 0 || sideBySide))
        {
            var column = Math.Max(GridWidth(members, maxRows), SubWords(group, members.Count));
            var left = sideBySide ? coreRight : coreLeft;
            var top = sideBySide || stackTop <= HeadingHeight ? HeadingHeight : stackTop + 32;   // stacked: room for the next group's heading
            headings.Add(new PlacedAreaBand(GroupWords(group), string.Empty, members.Count, new WholeRect(left, top - 24, column, 20), Sub: true));
            var next = Grid(members, left, top, placed, maxRows);
            coreBottom = Math.Max(coreBottom, next);
            stackTop = next;
            coreRight = sideBySide ? left + column + Gap : Math.Max(coreRight, left + column + Gap);
        }

        var coreWidth = Math.Max(Math.Max(coreRight - coreLeft - Gap, MinWidth), HeadingWords(Labels.ContextAreaCore, Labels.ContextAreaCoreWhat));
        headings.Add(new PlacedAreaBand(Labels.ContextAreaCore, Labels.ContextAreaCoreWhat, core.Count, new WholeRect(coreLeft, 0, coreWidth, 38), Sub: false));
        x = coreLeft + coreWidth + Gap;

        (x, bottom) = Outer(new OuterBand(data, Labels.ContextAreaData, Labels.ContextAreaDataWhat), x, Math.Max(bottom, coreBottom), sink);
        var contentBottom = Math.Max(bottom, coreBottom);
        var notesRight = AddNotes(sink.Notes, headings, contentBottom - 8);
        var worldWidth = Math.Max(x - Gap + Margin, notesRight > 0 ? notesRight + Margin : 0);
        var world = new WholeRect(0, 0, worldWidth, contentBottom + (sink.Notes.Count > 0 ? NoteHeight : 0) + Margin);
        return new WholeMapGeometry(world, ByArea: false, [], [], placed, [], Links(edges, placed), [], [], headings);
    }

    // An outer band (Entry, Data): one column as wide as its words, or the thin empty one.
    // The words of a Core sub-group. The switch lives here, not in Labels, so Labels never names a Whole type (ARCH-CYCLE-001).
    private static string GroupWords(CoreGroup group) => group switch
    {
        CoreGroup.Requests => Labels.ContextAreaGroupRequests,
        CoreGroup.Responses => Labels.ContextAreaGroupResponses,
        CoreGroup.Services => Labels.ContextAreaGroupServices,
        _ => Labels.ContextAreaGroupServices,
    };

    // The notes of the empty bands in one row from the margin; returns where the row ends (0 with no note).
    private static double AddNotes(List<string> notes, List<PlacedAreaBand> headings, double y)
    {
        var x = Margin;
        foreach (var title in notes)
        {
            var width = NoteWords(title);
            headings.Add(new PlacedAreaBand(title, string.Empty, 0, new WholeRect(x, y, width, 18), Sub: false));
            x += width + Gap + Gap;
        }

        return notes.Count > 0 ? x - Gap - Gap : 0;
    }

    // The line an empty band is drawn as: "Entry points: none in this area".
    private static double NoteWords(string title) => (title.Length + 2 + Labels.ContextAreaNone.Length) * CharWidth;

    private static (double Right, double Bottom) Outer(OuterBand band, double x, double bottom, Drawn sink)
    {
        var members = band.Members;
        if (members.Count == 0)
        {
            sink.Notes.Add(band.Title);   // an empty band takes no column: one muted line under the bands
            return (x, bottom);
        }

        var width = Math.Max(GridWidth(members, sink.MaxRows), HeadingWords(band.Title, band.Caption));
        sink.Headings.Add(new PlacedAreaBand(band.Title, band.Caption, members.Count, new WholeRect(x, 0, width, 38), Sub: false));
        var next = Grid([.. members.OrderBy(n => n.Label, StringComparer.Ordinal)], x, HeadingHeight, sink.Nodes, sink.MaxRows);
        return (x + width + Gap, Math.Max(bottom, next));
    }

    private static int ColumnsOf(int count, int maxRows) => Math.Max(1, (count + maxRows - 1) / maxRows);

    private static double GridWidth(List<WholeNode> members, int maxRows)
    {
        var columns = ColumnsOf(members.Count, maxRows);
        return (columns * WidthOf(members)) + ((columns - 1) * Gap);
    }

    // The words of a heading: the title on one line, the caption under it; the wider of the two.
    private static double HeadingWords(string title, string caption) => Math.Max(title.Length * TitleChar, caption.Length * CharWidth);

    private static double SubWords(CoreGroup group, int count) => ((GroupWords(group).Length + 1 + count.ToString(System.Globalization.CultureInfo.InvariantCulture).Length) * CharWidth) + 8;

    // A group as a grid: column by column in label order, at most MaxRows rows, as even as the columns allow. Returns the bottom.
    private static double Grid(List<WholeNode> members, double left, double top, List<PlacedNode> placed, int maxRows)
    {
        var columns = ColumnsOf(members.Count, maxRows);
        var rows = Math.Max(1, (members.Count + columns - 1) / columns);
        var width = WidthOf(members);
        for (var i = 0; i < members.Count; i++)
        {
            var node = members[i];
            var rect = new WholeRect(left + (i / rows * (width + Gap)), top + (i % rows * RowPitch), width, NodeHeight);
            placed.Add(new PlacedNode(node.Key, node.Label, node.Kind, 0, rect, node.Confirmed));
        }

        return top + (Math.Min(rows, members.Count) * RowPitch);
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

    private sealed record Drawn(List<PlacedNode> Nodes, List<PlacedAreaBand> Headings, int MaxRows)
    {
        internal List<string> Notes { get; } = [];
    }
}
