using System.Text.Json;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The read-only graph of one workflow revision: its nodes at the positions Sindri served, its edges as arrows and, when a run is given, each node's status.
/// The app does no layout: a node the server gave no position is not drawn (its id is in <see cref="Unplaced"/>), and nor is an edge that touches one.
/// </summary>
internal sealed class WorkflowGraphViewModel : ObservableObject
{
    /// <summary>A node card's width in the world.</summary>
    internal const double CardWidth = 200;

    /// <summary>A node card's height in the world.</summary>
    internal const double CardHeight = 64;

    /// <summary>The length of a missing branch's dotted stub.</summary>
    internal const double EndStub = 22;

    /// <summary>The width of the box a missing branch's words are drawn in.</summary>
    internal const double EndLabelWidth = 84;

    /// <summary>The height of the box a missing branch's words are drawn in.</summary>
    internal const double EndLabelHeight = 16;

    /// <summary>Builds the graph.</summary>
    /// <param name="workflow">The workflow as served.</param>
    /// <param name="statuses">The latest run's status word (as served) per node id, or null when no run is shown.</param>
    internal WorkflowGraphViewModel(WorkflowView workflow, IReadOnlyDictionary<string, string>? statuses = null)
    {
        Slug = workflow.Slug;
        _summaries = (workflow.Summaries ?? []).ToDictionary(x => x.Node, StringComparer.Ordinal);
        var positions = new Dictionary<string, GraphPoint>(StringComparer.Ordinal);
        foreach (var p in workflow.Positions)
        {
            positions[p.Node] = new GraphPoint(p.X, p.Y);
        }

        var nodes = new List<GraphNodeViewModel>();
        var unplaced = new List<string>();
        foreach (var (id, type, title) in ReadNodes(workflow.Spec))
        {
            if (!positions.TryGetValue(id, out var at))
            {
                unplaced.Add(id);
                continue;
            }

            var status = statuses is not null && statuses.TryGetValue(id, out var word) ? word : null;
            nodes.Add(new GraphNodeViewModel(id, title.Length > 0 ? title : id, type, at.X, at.Y, status));
        }

        Nodes = nodes;
        Unplaced = unplaced;
        Edges = BuildEdges(workflow.Spec, nodes);
        Ends = GraphEnds.Build(nodes, Edges);
        Order = [.. nodes.OrderBy(n => n.X).ThenBy(n => n.Y).ThenBy(n => n.Id, StringComparer.Ordinal)];
    }

    private readonly Dictionary<string, NodeSummaryView> _summaries;

    /// <summary>The selected step's title and what it does ("Every weekday at 7:00 · ..."), from the lines the API serves; empty when no step is selected or the API sent none for it.</summary>
    public string StepLine => SelectedId is { } id && _summaries.TryGetValue(id, out var line) && Nodes.FirstOrDefault(n => string.Equals(n.Id, id, StringComparison.Ordinal)) is { } node
        ? Labels.WfStepLine(node.Title, line.Summary, line.SummaryProblem)
        : string.Empty;

    /// <summary>Raised with the node's id when a node is selected or activated.</summary>
    internal event EventHandler<ValueEventArgs<string>>? NodeSelected;

    /// <summary>The workflow's slug: a canvas keeps its pan and zoom while the slug stays the same.</summary>
    internal string Slug { get; }

    /// <summary>The ids of nodes with no served position, in spec order: not drawn, so the page can name them.</summary>
    public IReadOnlyList<string> Unplaced { get; }

    /// <summary>True when some node has no served position.</summary>
    public bool HasUnplaced => Unplaced.Count > 0;

    /// <summary>The words for <see cref="Unplaced"/>, or empty when every node has a position.</summary>
    public string UnplacedText => Unplaced.Count == 0 ? string.Empty : Labels.WfGraphUnplaced(Unplaced);

    /// <summary>The nodes with a position, in the order the spec lists them.</summary>
    public IReadOnlyList<GraphNodeViewModel> Nodes { get; }

    /// <summary>The branches of condition steps that have no edge, each drawn as "false → done".</summary>
    public IReadOnlyList<GraphEnd> Ends { get; }

    /// <summary>The arrows; an edge naming a node the spec lacks is left out.</summary>
    public IReadOnlyList<GraphEdgeViewModel> Edges { get; }

    /// <summary>Gets the id of the selected node, or null.</summary>
    public string? SelectedId { get; private set { Set(ref field, value); OnChanged(nameof(StepLine)); } }

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The graph's accessible name.</summary>
    public string Name => Labels.WfGraphName;

    /// <summary>The words shown over an empty graph.</summary>
    public string EmptyText => Labels.WfGraphEmpty;
#pragma warning restore CA1822, MA0041

    /// <summary>Gets a value indicating whether the spec has no nodes at all; nodes without a position are not "no steps", the unplaced line names them.</summary>
    public bool IsEmpty => Nodes.Count == 0 && Unplaced.Count == 0;

    /// <summary>The nodes in position order: left to right, then top to bottom.</summary>
    internal IReadOnlyList<GraphNodeViewModel> Order { get; }

    /// <summary>The world's size: the far corner of the farthest card or missing branch's words.</summary>
    internal GraphPoint Extent => Nodes.Count == 0 ? default : new GraphPoint(Math.Max(Nodes.Max(n => n.X) + CardWidth, Ends.Select(e => e.Label.Right).DefaultIfEmpty().Max()), Math.Max(Nodes.Max(n => n.Y) + CardHeight, Ends.Select(e => e.Label.Bottom).DefaultIfEmpty().Max()));

    /// <summary>A graph read again keeps the node that was selected (by id), so a live refresh doesn't drop the keyboard's place.</summary>
    /// <param name="before">The graph shown until now, or null.</param>
    /// <param name="after">The graph just built.</param>
    /// <returns><paramref name="after"/>, with the same node selected when it still has one by that id.</returns>
    internal static WorkflowGraphViewModel Keeping(WorkflowGraphViewModel? before, WorkflowGraphViewModel after)
    {
        if (before?.SelectedId is { } id)
        {
            _ = after.SelectNode(id);
        }

        return after;
    }

    /// <summary>Selects a node and raises <see cref="NodeSelected"/>.</summary>
    /// <param name="id">The node's id.</param>
    /// <returns>False when no node has that id.</returns>
    internal bool SelectNode(string id)
    {
        if (Nodes.All(n => !string.Equals(n.Id, id, StringComparison.Ordinal)))
        {
            return false;
        }

        SelectedId = id;
        foreach (var node in Nodes)
        {
            node.IsSelected = string.Equals(node.Id, id, StringComparison.Ordinal);
        }

        NodeSelected?.Invoke(this, new ValueEventArgs<string>(id));
        return true;
    }

    /// <summary>Selects the next node in position order (the first when none is selected).</summary>
    /// <returns>False at the last node or an empty graph: nothing moved.</returns>
    internal bool SelectNext() => Step(1);

    /// <summary>Selects the previous node in position order (the first when none is selected).</summary>
    /// <returns>False at the first node or an empty graph: nothing moved.</returns>
    internal bool SelectPrevious() => Step(-1);

    /// <summary>Raises <see cref="NodeSelected"/> again for the selected node (Enter).</summary>
    /// <returns>False when no node is selected.</returns>
    internal bool ActivateSelected()
    {
        if (SelectedId is not { } id)
        {
            return false;
        }

        NodeSelected?.Invoke(this, new ValueEventArgs<string>(id));
        return true;
    }

    /// <summary>Raised when the Delete key asks to remove the selected node.</summary>
    internal event EventHandler<ValueEventArgs<string>>? RemoveRequested;

    /// <summary>Asks to remove the selected node (Delete).</summary>
    /// <returns>False when no node is selected.</returns>
    internal bool RequestRemoveSelected()
    {
        if (SelectedId is not { } id)
        {
            return false;
        }

        RemoveRequested?.Invoke(this, new ValueEventArgs<string>(id));
        return true;
    }

    private bool Step(int direction)
    {
        if (Order.Count == 0)
        {
            return false;
        }

        var current = SelectedId is null ? -1 : Order.ToList().FindIndex(n => string.Equals(n.Id, SelectedId, StringComparison.Ordinal));
        if (current < 0)
        {
            return SelectNode(Order[0].Id);
        }

        var next = current + direction;
        return next >= 0 && next < Order.Count && SelectNode(Order[next].Id);
    }

    private static List<(string Id, string Type, string Title)> ReadNodes(JsonElement spec)
    {
        var found = new List<(string, string, string)>();
        if (spec.ValueKind != JsonValueKind.Object || !spec.TryGetProperty("nodes", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return found;
        }

        foreach (var node in list.EnumerateArray())
        {
            if (Text(node, "id") is { Length: > 0 } id)
            {
                found.Add((id, Text(node, "type"), Text(node, "title")));
            }
        }

        return found;
    }

    private static List<GraphEdgeViewModel> BuildEdges(JsonElement spec, List<GraphNodeViewModel> nodes)
    {
        var edges = new List<GraphEdgeViewModel>();
        if (spec.ValueKind != JsonValueKind.Object || !spec.TryGetProperty("edges", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return edges;
        }

        var byId = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        foreach (var edge in list.EnumerateArray())
        {
            if (!byId.TryGetValue(Text(edge, "from"), out var from) || !byId.TryGetValue(Text(edge, "to"), out var to))
            {
                continue;
            }

            var output = Text(edge, "output");
            edges.Add(new GraphEdgeViewModel(
                from.Id,
                to.Id,
                output.Length == 0 ? null : output,
                new GraphPoint(from.X + CardWidth, from.Y + (CardHeight / 2)),
                new GraphPoint(to.X, to.Y + (CardHeight / 2))));
        }

        return edges;
    }

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}
