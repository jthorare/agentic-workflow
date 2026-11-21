namespace Aw.Sindri.Ui.ViewModels.Workflows;

#pragma warning disable MA0182 // the canvas takes it in the next commits of #3684
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

    private const double CardWidth = 290;
    private const double CardHeight = 60;
    private const double HGap = 56;
    private const double VGap = 24;
    private const double RowGap = 56;
    private const double ForkGap = 72;
    private const double PadLeft = 32;
    private const double PadRight = 80;
    private const double Margin = 24;

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
        double cardW = CardWidth * u, hGap = HGap * u, wide = ForkGap * u, padLeft = PadLeft * u, padRight = PadRight * u;
        var rows = new List<List<(IReadOnlyList<string> Layer, double X)>>();
        List<(IReadOnlyList<string> Layer, double X)> current = [];
        var x = padLeft;
        var previousGap = hGap;
        foreach (var layer in input.Layers)
        {
            var next = current.Count == 0 ? padLeft : x + cardW + previousGap;
            if (current.Count > 0 && next + cardW > width - padRight + 0.5)
            {
                rows.Add(current);
                current = [];
                next = padLeft;
            }

            current.Add((layer, next));
            x = next;
            previousGap = layer.Any(input.Forks.Contains) ? wide : hGap;
        }

        rows.Add(current);
        return rows;
    }

    // Packs the layers into rows: a layer wraps when it would reach past the right pad. Laid out at the text scale; the zoom only scales the whole.
    private static GraphFitResult Lay(GraphFitInput input, double width, double u, double zoom)
    {
        double cardW = CardWidth * u, cardH = CardHeight * u, vGap = VGap * u, rowGap = RowGap * u, padRight = PadRight * u;
        var rows = Pack(input, width, u);
        var cards = new Dictionary<string, GraphBox>(StringComparer.Ordinal);
        var y = Margin * u;
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

        return new GraphFitResult(zoom, cards, rows.Max(r => r.Count), rows.Count, right + padRight, y - rowGap + (Margin * u));
    }
}
