namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The card sizes and gaps, and the wrap layout the canvas asks for (<see cref="WorkflowGraphViewModel"/>, continued; #3684).</summary>
internal sealed partial class WorkflowGraphViewModel
{
    /// <summary>A node card's width in the world.</summary>
    internal const double CardWidth = 290;

    /// <summary>A node card's height in the world.</summary>
    internal const double CardHeight = 60;

    /// <summary>A card's height when its settings line is longer than <see cref="SettingsWrapAt"/> characters and wraps to a second line.</summary>
    internal const double CardTallHeight = 80;

    /// <summary>The settings line length above which a card is <see cref="CardTallHeight"/> tall.</summary>
    internal const int SettingsWrapAt = 32;

    /// <summary>The gap between two columns of cards in a wrapped row.</summary>
    internal const double ColumnGap = 56;

    /// <summary>The gap between two cards stacked in one layer.</summary>
    internal const double StackGap = 24;

    /// <summary>The gap between two rows of a wrapped graph.</summary>
    internal const double RowGap = 56;

    /// <summary>The gap after a layer that holds a fork, wide enough for the branch words clear of the ports.</summary>
    internal const double ForkGap = 72;

    /// <summary>The space left of the first column.</summary>
    internal const double LeftPad = 32;

    /// <summary>The space right of the last column, where a return line runs.</summary>
    internal const double RightPad = 80;

    /// <summary>The margin above the first row and below the last.</summary>
    internal const double EdgeMargin = 24;

    /// <summary>How many times the cards were laid out: zoom buttons must never raise it, only open, a size change and a text-size change.</summary>
    internal int RelayoutCount { get; private set; }

    /// <summary>Puts every card where <see cref="GraphFit"/> wraps it for this canvas; the served positions then only give the layer order. Edges and missing-branch words follow the cards.</summary>
    /// <param name="canvasWidth">The canvas clip's width in pixels.</param>
    /// <param name="canvasHeight">The canvas clip's height in pixels.</param>
    /// <param name="textScale">The text size setting as a factor.</param>
    /// <returns>The Fit zoom for the new layout.</returns>
    internal double Relayout(double canvasWidth, double canvasHeight, double textScale)
    {
        RelayoutCount++;
        var fit = GraphFit.Compute(new GraphFitInput(_layers, [.. Edges.Select(e => (e.From, e.To))], Heights()), canvasWidth, canvasHeight, textScale);
        foreach (var node in Nodes)
        {
            if (fit.Cards.TryGetValue(node.Id, out var card))
            {
                node.X = card.X;
                node.Y = card.Y;
                node.Width = card.Width;
                node.Height = card.Height;
                node.Scale = textScale;
            }
        }

        Edges = BuildEdges(_spec, [.. Nodes]);
        foreach (var edge in Edges)
        {
            edge.Route = GraphFit.Route(fit, edge.From, edge.To, edge.Start, edge.End);
        }

        Ends = GraphEnds.Build([.. Nodes], Edges, SpecBranches(_spec));
        return fit.Zoom;
    }

    /// <summary>The chip at the right end of a card's title line: "off" on the Schedule card of a workflow that is off; null for every other card (no next-run time is served yet).</summary>
    /// <param name="node">The card.</param>
    /// <returns>The chip's word, or null for none.</returns>
    internal string? ChipFor(GraphNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _isOff && string.Equals(node.Type, "schedule", StringComparison.Ordinal) ? Labels.WfState(active: false).Word.ToLowerInvariant() : null;
    }

    // A card whose settings line is longer than SettingsWrapAt wraps it to a second line, so it is taller; the line is the card's line 2 (GraphCardLine.Settings).
    private Dictionary<string, double> Heights() =>
        Nodes.ToDictionary(n => n.Id, n => n.Line.Length > SettingsWrapAt ? CardTallHeight : CardHeight, StringComparer.Ordinal);
}
