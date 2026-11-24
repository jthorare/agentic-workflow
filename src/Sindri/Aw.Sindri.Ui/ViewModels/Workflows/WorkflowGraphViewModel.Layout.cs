namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The card sizes and gaps, and the wrap layout the canvas asks for (<see cref="WorkflowGraphViewModel"/>, continued; #3684).</summary>
internal sealed partial class WorkflowGraphViewModel
{
    /// <summary>A node card's width in the world.</summary>
    internal const double CardWidth = 290;

    /// <summary>A node card's height in the world.</summary>
    internal const double CardHeight = 60;

    /// <summary>A two-line card's height, for a layout given per-card heights (GraphFitInput). The canvas draws one settings line on every card, cut with an
    /// ellipsis and whole in the card's tooltip, so it lays every card out <see cref="CardHeight"/> tall and a row's cards match (#4203).</summary>
    internal const double CardTallHeight = 80;

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
        var fit = GraphFit.Compute(new GraphFitInput(_layers, [.. Edges.Select(e => (e.From, e.To))]), canvasWidth, canvasHeight, textScale);   // every card one line tall (#4203)
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

    /// <summary>When the workflow's schedule fires next, from the list's row (#3758); null when none is known.</summary>
    internal DateTimeOffset? NextRunAt { get; set; }

    /// <summary>The clock and time zone the next-run chip reads (the page's clock; a test sets a fake one).</summary>
    internal TimeProvider Clock { get; set; } = TimeProvider.System;

    /// <summary>The chip at the right end of a card's title line, on the Schedule card only: "off" for a workflow that is off; for one that is on, its next run
    /// in the clock's local time ("next 7:00" within 24 hours, else "next Mon 7:00"), or nothing while no next run is known (#3758).</summary>
    /// <param name="node">The card.</param>
    /// <returns>The chip's words, or null for none.</returns>
    internal string? ChipFor(GraphNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!string.Equals(node.Type, "schedule", StringComparison.Ordinal))
        {
            return null;
        }

        if (_isOff)
        {
            return Labels.WfState(active: false).Word.ToLowerInvariant();
        }

        var now = Clock.GetUtcNow();
        return NextRunAt is { } next && next > now   // a time at or before now is not next any more (#4188)
            ? Labels.WfNextRun(TimeZoneInfo.ConvertTime(next, Clock.LocalTimeZone), soon: next - now < TimeSpan.FromHours(24))
            : null;
    }
}
