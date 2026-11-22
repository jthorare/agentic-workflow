namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// Places the server's layers into rows that wrap at the window's width and finds the zoom that fits the whole graph (#3684). A pure function: no Avalonia,
/// no server positions changed. Fit never goes above 100 % and never below the zoom where card text is 12 px.
/// </summary>
internal static class GraphFit
{
    /// <summary>The smallest card text Fit allows, in pixels.</summary>
    internal const double MinTextPx = 12;

    /// <summary>The card text's size at 100 % zoom and 100 % text size, in pixels.</summary>
    internal const double CardTextPx = 14;


    /// <summary>The lowest Fit zoom: the zoom at which card text is <see cref="MinTextPx"/>.</summary>
    /// <param name="textScale">The text size setting as a factor (1 = 100 %, 1.3 = 130 %).</param>
    /// <returns>The zoom, rounded up to a whole percent.</returns>
    internal static double Floor(double textScale) => Math.Ceiling((MinTextPx / (CardTextPx * textScale) * 100) - 1e-9) / 100;

    /// <summary>Lays the layers out and picks the Fit zoom.</summary>
    /// <param name="input">The layers.</param>
    /// <param name="canvasWidth">The canvas clip's width in pixels.</param>
    /// <param name="canvasHeight">The canvas clip's height in pixels (the zoom band is not part of it).</param>
    /// <param name="textScale">The text size setting as a factor.</param>
    /// <returns>The layout and its Fit zoom.</returns>
    internal static GraphFitResult Compute(GraphFitInput input, double canvasWidth, double canvasHeight, double textScale)
    {
        ArgumentNullException.ThrowIfNull(input);
        var floor = Floor(textScale);
        var percent = 100;
        while (true)
        {
            var zoom = percent / 100.0;
            var result = Lay(input, canvasWidth / zoom, textScale, zoom);
            if (zoom <= floor + 1e-9 || (result.Width * zoom <= canvasWidth + 0.5 && result.Height * zoom <= canvasHeight && ArmsBeside(input, result)))
            {
                return result;
            }

            percent--;
        }
    }

    // A fork's arms stay in the fork's row: a layout that wraps them onto the next row is not a fit, so Fit tries a smaller zoom.
    private static bool ArmsBeside(GraphFitInput input, GraphFitResult result) =>
        input.Forks.All(fork => input.Edges.Where(e => string.Equals(e.From, fork, StringComparison.Ordinal)).All(e => result.RowOf[e.To] == result.RowOf[fork]));

    // The margins and the left pad are screen pixels (they do not scale with the zoom), so in the world they are divided by it.
    private static List<List<(IReadOnlyList<string> Layer, double X)>> Pack(GraphFitInput input, double width, double u, double zoom)
    {
        double cardW = WorkflowGraphViewModel.CardWidth * u, hGap = WorkflowGraphViewModel.ColumnGap * u, wide = WorkflowGraphViewModel.ForkGap * u, padLeft = WorkflowGraphViewModel.LeftPad / zoom, padRight = WorkflowGraphViewModel.RightPad * u;
        var rows = new List<List<(IReadOnlyList<string> Layer, double X)>>();
        List<(IReadOnlyList<string> Layer, double X)> current = [];
        var x = padLeft;
        var previousGap = hGap;
        var forks = input.Forks;
        var limit = width - padRight + 0.5;
        foreach (var layer in input.Layers)
        {
            var isFork = layer.Any(forks.Contains);
            var next = current.Count == 0 ? padLeft : x + cardW + previousGap;
            if (current.Count > 0 && next + cardW > limit)
            {
                rows.Add(current);
                current = [];
                next = padLeft;
            }

            current.Add((layer, next));
            x = next;
            previousGap = isFork ? wide : hGap;
        }

        rows.Add(current);
        return rows;
    }

    // Packs the layers into rows: a layer wraps when it would reach past the right pad. Laid out at the text scale; the zoom only scales the whole.
    private static GraphFitResult Lay(GraphFitInput input, double width, double u, double zoom)
    {
        double cardW = WorkflowGraphViewModel.CardWidth * u, vGap = WorkflowGraphViewModel.StackGap * u, rowGap = WorkflowGraphViewModel.RowGap * u, padRight = WorkflowGraphViewModel.RightPad * u;
        var rows = Pack(input, width, u, zoom);
        var rowOf = RowsOf(rows);
        var cards = new Dictionary<string, GraphBox>(StringComparer.Ordinal);
        var margin = WorkflowGraphViewModel.EdgeMargin / zoom;
        var y = margin;
        var right = 0.0;
        List<(double Top, double Height, double Right)> bands = [];
        foreach (var row in rows)
        {
            double Stack(IReadOnlyList<string> l) => l.Sum(id => HeightOf(input, id, u)) + ((l.Count - 1) * vGap);
            var rowHeight = row.Max(c => Stack(c.Layer));
            foreach (var (layer, lx) in row)
            {
                var top = y + ((rowHeight - Stack(layer)) / 2);
                foreach (var id in layer)
                {
                    var cardH = HeightOf(input, id, u);
                    cards[id] = new GraphBox(lx, top, cardW, cardH);
                    top += cardH + vGap;
                }

                right = Math.Max(right, lx + cardW);
            }

            bands.Add((y, rowHeight, row.Max(c => c.X) + cardW));
            y += rowHeight + rowGap;
        }

        return new GraphFitResult(zoom, cards, rows.Max(r => r.Count), rows.Count, right + padRight, y - rowGap + margin, Lanes(bands, u, zoom), rowOf);
    }

    private static Dictionary<string, int> RowsOf(List<List<(IReadOnlyList<string> Layer, double X)>> rows) =>
        rows.SelectMany((row, r) => row.SelectMany(c => c.Layer).Select(id => (id, r))).ToDictionary(p => p.id, p => p.r, StringComparer.Ordinal);

    private static double HeightOf(GraphFitInput input, string id, double u) => (input.Heights is not null && input.Heights.TryGetValue(id, out var h) ? h : WorkflowGraphViewModel.CardHeight) * u;

    // The line that runs back from the end of a row to the start of the next: down the right pad, left along the row gap, down the left pad. Each part is a thin box.
    private static List<GraphBox> Lanes(List<(double Top, double Height, double Right)> bands, double u, double zoom)
    {
        const double thick = 2;
        List<GraphBox> lanes = [];
        for (var r = 0; r + 1 < bands.Count; r++)
        {
            var (top, height, right) = bands[r];
            var gapY = top + height + (WorkflowGraphViewModel.RowGap * u / 2);
            var outX = right + (WorkflowGraphViewModel.ReturnLaneOffset * u);
            var inX = WorkflowGraphViewModel.LeftPad / zoom / 2;
            var nextMid = bands[r + 1].Top + (bands[r + 1].Height / 2);
            lanes.Add(new GraphBox(outX, top + (height / 2), thick, gapY - top - (height / 2)));
            lanes.Add(new GraphBox(inX, gapY, outX - inX, thick));
            lanes.Add(new GraphBox(inX, gapY, thick, nextMid - gapY));
        }

        return lanes;
    }
}
