namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>
/// How a run that ended badly is drawn (F3 spec 3 §8.5): the edge to the end card leaves the stage that went wrong, red when that stage failed
/// and solid otherwise. Pure: it takes the graph the builder made and returns the one to draw.
/// </summary>
internal static class RunEndStates
{
    /// <summary>The stage that went wrong: the first one that failed, else the first that did not work out or was cancelled; null when none did.</summary>
    /// <param name="graph">The graph.</param>
    /// <returns>The stage, or null.</returns>
    internal static RunNode? BadStage(RunGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var stages = graph.Nodes.Where(n => n.Kind == RunNodeKind.Stage).ToList();
        return stages.Find(n => n.State == RunNodeState.Failed) ?? stages.Find(n => RunStates.EndedBadly(n.State));
    }

    /// <summary>Moves the end edges to leave the stage that went wrong.</summary>
    /// <param name="graph">The graph from the builder.</param>
    /// <returns>The graph to draw; the same one when no stage went wrong.</returns>
    internal static RunGraph Apply(RunGraph graph)
    {
        if (BadStage(graph) is not { } bad)
        {
            return graph;
        }

        var tone = bad.State == RunNodeState.Failed ? EdgeTone.Failed : EdgeTone.Normal;
        var edges = graph.Edges.Where(e => e.Kind != RunEdgeKind.End).ToList();
        edges.AddRange(graph.Edges.Where(e => e.Kind == RunEdgeKind.End).Select(e => e with { From = bad.Id, Tone = tone }));
        return graph with { Edges = edges };
    }
}
