using System.Text.Json;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The read-only graph of one workflow revision: its nodes at the positions Sindri served, its edges as arrows and, when a run is given, each node's status.
/// A node the server gave no position is placed after the rightmost one (its left edge one card width plus <see cref="Gap"/> right of it), at the top row.
/// </summary>
internal sealed class WorkflowGraphViewModel : ObservableObject
{
    /// <summary>A node card's width in the world.</summary>
    internal const double CardWidth = 200;

    /// <summary>A node card's height in the world.</summary>
    internal const double CardHeight = 64;

    /// <summary>The space between a placed card and the next unplaced one.</summary>
    internal const double Gap = 80;

    private string? _selectedId;

    /// <summary>Builds the graph.</summary>
    /// <param name="workflow">The workflow as served.</param>
    /// <param name="statuses">The latest run's status word (as served) per node id, or null when no run is shown.</param>
    internal WorkflowGraphViewModel(WorkflowView workflow, IReadOnlyDictionary<string, string>? statuses = null)
    {
        var positions = new Dictionary<string, GraphPoint>(StringComparer.Ordinal);
        foreach (var p in workflow.Positions)
        {
            positions[p.Node] = new GraphPoint(p.X, p.Y);
        }

        var raw = ReadNodes(workflow.Spec);
        var cursor = raw.Select(n => positions.TryGetValue(n.Id, out var at) ? at.X + CardWidth : double.NegativeInfinity).DefaultIfEmpty(double.NegativeInfinity).Max();
        var nextFree = double.IsNegativeInfinity(cursor) ? 0 : cursor + Gap;

        var nodes = new List<GraphNodeViewModel>();
        foreach (var (id, type, title) in raw)
        {
            if (!positions.TryGetValue(id, out var at))
            {
                at = new GraphPoint(nextFree, 0);
                nextFree += CardWidth + Gap;
            }

            var status = statuses is not null && statuses.TryGetValue(id, out var word) ? word : null;
            nodes.Add(new GraphNodeViewModel(id, title.Length > 0 ? title : id, type, at.X, at.Y, status));
        }

        Nodes = nodes;
        Edges = BuildEdges(workflow.Spec, nodes);
        Order = [.. nodes.OrderBy(n => n.X).ThenBy(n => n.Y).ThenBy(n => n.Id, StringComparer.Ordinal)];
    }

    /// <summary>Raised with the node's id when a node is selected or activated.</summary>
    internal event EventHandler<string>? NodeSelected;

    /// <summary>The nodes, in the order the spec lists them.</summary>
    public IReadOnlyList<GraphNodeViewModel> Nodes { get; }

    /// <summary>The arrows; an edge naming a node the spec lacks is left out.</summary>
    public IReadOnlyList<GraphEdgeViewModel> Edges { get; }

    /// <summary>Gets the id of the selected node, or null.</summary>
    public string? SelectedId
    {
        get => _selectedId;
        private set => Set(ref _selectedId, value);
    }

    /// <summary>The graph's accessible name.</summary>
    public string Name => Labels.WfGraphName;

    /// <summary>The words shown over an empty graph.</summary>
    public string EmptyText => Labels.WfGraphEmpty;

    /// <summary>Gets a value indicating whether the workflow has no nodes.</summary>
    public bool IsEmpty => Nodes.Count == 0;

    /// <summary>The nodes in position order: left to right, then top to bottom.</summary>
    internal IReadOnlyList<GraphNodeViewModel> Order { get; }

    /// <summary>The world's size: the far corner of the farthest card.</summary>
    internal GraphPoint Extent => Nodes.Count == 0 ? default : new GraphPoint(Nodes.Max(n => n.X) + CardWidth, Nodes.Max(n => n.Y) + CardHeight);

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

        NodeSelected?.Invoke(this, id);
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

        NodeSelected?.Invoke(this, id);
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
