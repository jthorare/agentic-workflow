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
            if (zoom <= floor + 1e-9 || (result.Width * zoom <= canvasWidth + 0.5 && result.Height * zoom <= canvasHeight))
            {
                return result;
            }

            percent--;
        }
    }

    private static List<List<(IReadOnlyList<string> Layer, double X)>> Pack(GraphFitInput input, double width, double u)
    {
        double cardW = WorkflowGraphViewModel.CardWidth * u, hGap = WorkflowGraphViewModel.ColumnGap * u, wide = WorkflowGraphViewModel.ForkGap * u, padLeft = WorkflowGraphViewModel.LeftPad * u, padRight = WorkflowGraphViewModel.RightPad * u;
        var rows = new List<List<(IReadOnlyList<string> Layer, double X)>>();
        List<(IReadOnlyList<string> Layer, double X)> current = [];
        var x = padLeft;
        var previousGap = hGap;
        var forks = input.Forks;
        var limit = width - padRight + 0.5;
        for (var k = 0; k < input.Layers.Count; k++)
        {
            var layer = input.Layers[k];
            var isFork = layer.Any(forks.Contains);
            var next = current.Count == 0 ? padLeft : x + cardW + previousGap;
            // a fork's arms are the next layer: they stay in the fork's row, so a fork whose arms would not fit beside it starts the next row
            var armsEnd = next + cardW + wide + cardW;
            if (current.Count > 0 && (next + cardW > limit || (isFork && k + 1 < input.Layers.Count && armsEnd > limit)))
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
        double cardW = WorkflowGraphViewModel.CardWidth * u, cardH = WorkflowGraphViewModel.CardHeight * u, vGap = WorkflowGraphViewModel.StackGap * u, rowGap = WorkflowGraphViewModel.RowGap * u, padRight = WorkflowGraphViewModel.RightPad * u;
        var rows = Pack(input, width, u);
        var cards = new Dictionary<string, GraphBox>(StringComparer.Ordinal);
        var y = WorkflowGraphViewModel.EdgeMargin * u;
        var right = 0.0;
        foreach (var row in rows)
        {
            double Stack(IReadOnlyList<string> l) => (l.Count * cardH) + ((l.Count - 1) * vGap);
            var rowHeight = row.Max(c => Stack(c.Layer));
            foreach (var (layer, lx) in row)
            {
                var top = y + ((rowHeight - Stack(layer)) / 2);
                foreach (var id in layer)
                {
                    cards[id] = new GraphBox(lx, top, cardW, cardH);
                    top += cardH + vGap;
                }

                right = Math.Max(right, lx + cardW);
            }

            y += rowHeight + rowGap;
        }

        return new GraphFitResult(zoom, cards, rows.Max(r => r.Count), rows.Count, right + padRight, y - rowGap + (WorkflowGraphViewModel.EdgeMargin * u));
    }
}
