namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>Where a condition's missing branches draw their dotted stub and "→ done" words.</summary>
internal static class GraphEnds
{
    /// <summary>Builds the ends of every condition branch that has no edge.</summary>
    /// <param name="nodes">The step cards.</param>
    /// <param name="edges">The edges that are drawn.</param>
    /// <param name="specBranches">Every (from, output) the spec has an edge for, drawn or not.</param>
    /// <returns>One end per missing branch.</returns>
    internal static List<GraphEnd> Build(List<GraphNodeViewModel> nodes, IReadOnlyList<GraphEdgeViewModel> edges, IReadOnlySet<(string From, string Branch)> specBranches)
    {
        _ = specBranches;   // red stub (#3655): the fix skips a branch the spec has even when its target is not drawn
        var ends = new List<GraphEnd>();
        var cards = nodes.Select(n => new GraphBox(n.X, n.Y, WorkflowGraphViewModel.CardWidth, WorkflowGraphViewModel.CardHeight)).ToList();
        foreach (var node in nodes.Where(n => string.Equals(n.Type, "condition", StringComparison.Ordinal)))
        {
            foreach (var (branch, across) in new[] { ("true", 50.0), ("false", WorkflowGraphViewModel.CardWidth - 50) })
            {
                if (!edges.Any(e => string.Equals(e.From, node.Id, StringComparison.Ordinal) && string.Equals(e.Label, branch, StringComparison.Ordinal)))
                {
                    ends.Add(PlaceEnd(node, branch + " \u2192 done", across, cards));
                }
            }
        }

        return ends;
    }

    /// <summary>Puts a missing branch's stub and words under the card, above it when a card is there, and right of it as the last choice.</summary>
    /// <param name="node">The condition step.</param>
    /// <param name="text">The words.</param>
    /// <param name="across">How far along the card the stub leaves it: true on the left half, false on the right, so both fit side by side.</param>
    /// <param name="cards">Every step card.</param>
    /// <returns>The first place whose stub and words clear every card and stay inside the world (Y at or below 0); when none does, the words under the card, pushed down past every card in their way.</returns>
    private static GraphEnd PlaceEnd(GraphNodeViewModel node, string text, double across, List<GraphBox> cards)
    {
        var x = node.X + across;
        var below = new GraphPoint(x, node.Y + WorkflowGraphViewModel.CardHeight);
        var above = new GraphPoint(x, node.Y);
        var right = new GraphPoint(node.X + WorkflowGraphViewModel.CardWidth, node.Y + (WorkflowGraphViewModel.CardHeight / 2));
        GraphEnd[] places =
        [
            new(node.Id, text, below, below with { Y = below.Y + WorkflowGraphViewModel.EndStub }, new GraphBox(x - (WorkflowGraphViewModel.EndLabelWidth / 2), below.Y + WorkflowGraphViewModel.EndStub + 2, WorkflowGraphViewModel.EndLabelWidth, WorkflowGraphViewModel.EndLabelHeight)),
            new(node.Id, text, above, above with { Y = above.Y - WorkflowGraphViewModel.EndStub }, new GraphBox(x - (WorkflowGraphViewModel.EndLabelWidth / 2), above.Y - WorkflowGraphViewModel.EndStub - 2 - WorkflowGraphViewModel.EndLabelHeight, WorkflowGraphViewModel.EndLabelWidth, WorkflowGraphViewModel.EndLabelHeight)),
            new(node.Id, text, right, right with { X = right.X + WorkflowGraphViewModel.EndStub }, new GraphBox(right.X + WorkflowGraphViewModel.EndStub + 6, right.Y - (WorkflowGraphViewModel.EndLabelHeight / 2), WorkflowGraphViewModel.EndLabelWidth, WorkflowGraphViewModel.EndLabelHeight)),
        ];
        return places.FirstOrDefault(p => p.Label.Y >= 0 && !cards.Exists(c => c.Overlaps(p.Label) || c.Overlaps(StubBox(p)))) ?? Lowered(places[0], cards);   // why Y >= 0: the world starts at 0, so words above a top-row card would be clipped
    }

    private static GraphEnd Lowered(GraphEnd below, List<GraphBox> cards)
    {
        var label = below.Label;
        for (var i = cards.FindIndex(c => c.Overlaps(label)); i >= 0; i = cards.FindIndex(c => c.Overlaps(label)))
        {
            label = label with { Y = cards[i].Bottom + 2 };
        }

        return below with { To = below.To with { Y = label.Y - 2 }, Label = label };
    }

    private static GraphBox StubBox(GraphEnd end) =>
        new(Math.Min(end.At.X, end.To.X), Math.Min(end.At.Y, end.To.Y), Math.Max(Math.Abs(end.To.X - end.At.X), 1), Math.Max(Math.Abs(end.To.Y - end.At.Y), 1));
}
