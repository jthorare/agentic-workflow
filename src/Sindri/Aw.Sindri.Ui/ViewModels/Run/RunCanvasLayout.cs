using Aw.Sindri.Ui.ViewModels.Context.Layout;

namespace Aw.Sindri.Ui.ViewModels.Run;

#pragma warning disable MA0182 // until slice 3 places the run canvas on its page

/// <summary>Places the run canvas's cards (F3 spec 3 §2, "Deterministic layout"). Pure: the heights come in measured, nothing is measured here.</summary>
internal static class RunCanvasLayout
{
    /// <summary>The width of a stage card.</summary>
    internal const double StageWidth = 320;

    /// <summary>The width of a side or end card.</summary>
    internal const double SideWidth = 280;

    /// <summary>The space between cards down the main path, and above the end row.</summary>
    internal const double Gap = 104;

    /// <summary>The distance between column centres: half a stage, half a side card and 170.</summary>
    internal const double Pitch = (StageWidth / 2) + (SideWidth / 2) + 170;

    /// <summary>The height a card gets when none was measured.</summary>
    internal const double DefaultHeight = 160;

    /// <summary>How far above the bottom of the card before its anchor a loop card sits.</summary>
    private const double LoopRise = 60;

    /// <summary>The space kept round the cards in the world.</summary>
    private const double WorldPadding = 120;

    /// <summary>The space between loop cards stacked in the same column.</summary>
    private const double StackGap = 40;

    /// <summary>The place of every card of a graph.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="heights">The measured height of each card by id; a card not in it gets <see cref="DefaultHeight"/>.</param>
    /// <returns>The layout.</returns>
    internal static RunLayout Of(RunGraph graph, IReadOnlyDictionary<string, double> heights)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(heights);
        double HeightOf(RunNode node) => heights.TryGetValue(node.Id, out var h) ? h : DefaultHeight;

        var rects = new Dictionary<string, MapRect>(StringComparer.Ordinal);
        var main = graph.Nodes.Where(n => n.Kind == RunNodeKind.Stage).OrderBy(n => n.Order ?? int.MaxValue).ThenBy(n => n.Id, StringComparer.Ordinal).ToList();
        var y = 0d;
        foreach (var stage in main)
        {
            var h = HeightOf(stage);
            rects[stage.Id] = new MapRect(-StageWidth / 2, y, StageWidth, h);
            y += h + Gap;
        }

        PlaceSides(graph, main, rects, HeightOf);
        var columns = PlaceEnds(graph, main, rects, HeightOf);
        return new RunLayout(rects, World(rects.Values), Ports(graph, columns));
    }

    private static void PlaceSides(RunGraph graph, List<RunNode> main, Dictionary<string, MapRect> rects, Func<RunNode, double> heightOf)
    {
        var stackedBottom = double.NegativeInfinity;
        foreach (var side in graph.Nodes.Where(n => n.Kind == RunNodeKind.Side))
        {
            var top = AnchorTop(graph, main, rects, side) ?? 0;
            top = Math.Max(top, stackedBottom + StackGap);
            var h = heightOf(side);
            rects[side.Id] = new MapRect(Pitch - (SideWidth / 2), top, SideWidth, h);
            stackedBottom = top + h;
        }
    }

    // The loop card sits LoopRise above the bottom of the card before the one it loops with (the prototype's Redesign: beside the gap above Plan).
    private static double? AnchorTop(RunGraph graph, List<RunNode> main, Dictionary<string, MapRect> rects, RunNode side)
    {
        var anchorId = side.BranchOf ?? graph.Edges.FirstOrDefault(e => string.Equals(e.From, side.Id, StringComparison.Ordinal))?.To;
        var at = main.FindIndex(n => string.Equals(n.Id, anchorId, StringComparison.Ordinal));
        if (at < 0)
        {
            return main.Count == 0 ? null : rects[main[0].Id].Y;
        }

        return at == 0 ? rects[main[0].Id].Y : rects[main[at - 1].Id].Bottom - LoopRise;
    }

    private static Dictionary<string, int> PlaceEnds(RunGraph graph, List<RunNode> main, Dictionary<string, MapRect> rects, Func<RunNode, double> heightOf)
    {
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        var row = main.Count == 0 ? 0 : rects[main[^1].Id].Bottom + Gap;
        var used = new EndColumns();
        foreach (var end in graph.Nodes.Where(n => n.Kind == RunNodeKind.End))
        {
            var column = ColumnOf(end.Outcome, used);
            columns[end.Id] = column;
            rects[end.Id] = new MapRect((column * Pitch) - (SideWidth / 2), row, SideWidth, heightOf(end));
        }

        return columns;
    }

    // A success sits under the main path (the first one; later ones continue to the right), what could not finish to the right, a stop or an unknown end to the left.
    private static int ColumnOf(string? outcome, EndColumns used)
    {
        var success = Is(outcome, "resolved") || Is(outcome, "alreadySatisfied");
        if (success && !used.Centre)
        {
            used.Centre = true;
            return 0;
        }

        var toTheRight = success || Is(outcome, "unresolved") || Is(outcome, "failed");
        return toTheRight ? ++used.Right : -++used.Left;
    }

    private static bool Is(string? outcome, string word) => string.Equals(outcome, word, StringComparison.Ordinal);

    private static Dictionary<string, EdgePorts> Ports(RunGraph graph, Dictionary<string, int> columns)
    {
        var kinds = graph.Nodes.ToDictionary(n => n.Id, n => n.Kind, StringComparer.Ordinal);
        var ports = new Dictionary<string, EdgePorts>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges)
        {
            var to = kinds.GetValueOrDefault(edge.To);
            var from = kinds.GetValueOrDefault(edge.From);
            ports[edge.Key] = to == RunNodeKind.Side ? new EdgePorts(PortSide.Right, PortSide.Bottom)
                : from == RunNodeKind.Side ? new EdgePorts(PortSide.Left, PortSide.Top)
                : to == RunNodeKind.End ? new EdgePorts(SideOfColumn(columns.GetValueOrDefault(edge.To)), PortSide.Top)
                : new EdgePorts(PortSide.Bottom, PortSide.Top);
        }

        return ports;
    }

    private static PortSide SideOfColumn(int column) => column switch
    {
        0 => PortSide.Bottom,
        > 0 => PortSide.Right,
        _ => PortSide.Left,
    };

    private static MapRect World(IEnumerable<MapRect> rects)
    {
        double left = double.MaxValue, top = double.MaxValue, right = double.MinValue, bottom = double.MinValue;
        var any = false;
        foreach (var r in rects)
        {
            any = true;
            left = Math.Min(left, r.Left);
            top = Math.Min(top, r.Top);
            right = Math.Max(right, r.Right);
            bottom = Math.Max(bottom, r.Bottom);
        }

        return any ? new MapRect(left - WorldPadding, top - WorldPadding, right - left + (2 * WorldPadding), bottom - top + (2 * WorldPadding)) : default;
    }

    private sealed class EndColumns
    {
        internal bool Centre { get; set; }

        internal int Right { get; set; }

        internal int Left { get; set; }
    }
}
#pragma warning restore MA0182
