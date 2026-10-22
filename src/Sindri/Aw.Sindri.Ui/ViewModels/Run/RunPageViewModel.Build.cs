using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Context.Layout;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The run page's answer to a read (<see cref="RunPageViewModel"/>, continued): the graph, its places, the cards, edges and header.</summary>
internal sealed partial class RunPageViewModel
{
    private bool _opened;   // the canvas has been sent to the active stage once for this run

    /// <summary>Where the cards are in the world.</summary>
    internal RunLayout Layout { get; private set; } = new(new Dictionary<string, MapRect>(StringComparer.Ordinal), default, new Dictionary<string, EdgePorts>(StringComparer.Ordinal));

    private void Show(InstanceDetail line, RecipeView? recipe, IReadOnlyDictionary<string, InstanceDetail> children, IReadOnlyList<InstanceSummary> runs)
    {
        var graph = RunGraphBuilder.Build(line, recipe, children);
        Layout = RunCanvasLayout.Of(graph, graph.Nodes.ToDictionary(n => n.Id, RunCardSizes.Of, StringComparer.Ordinal));
        var active = ActiveOf(graph);
        var stages = graph.Nodes.Where(n => n.Kind == RunNodeKind.Stage).ToList();
        var cards = graph.Nodes.Select(n => Card(n, line, stages, active)).ToList();
        (Edges, Pills) = EdgesOf(graph);
        World = Layout.World;
        ActiveNodeId = active;
        ReadingOrder = [.. cards.Where(c => c.IsStage).SelectMany(c => c.Steps.Count > 0 ? c.Steps.Select(s => s.Key) : [c.Id])];
        BranchesNote = graph.BranchesServed ? string.Empty : Labels.RunBranchesNotServed;
        OnChanged(nameof(HasBranchesNote));
        Header(line, graph, runs);
        ShowRuns(runs, line.Address);
        Cards = cards;   // last of the drawn parts: the canvas redraws when the cards change, and reads the rest then
        _watched = new HashSet<string>([line.Address, .. children.Values.Select(c => c.Address), .. graph.Nodes.Select(n => n.ChildAddress).OfType<string>()], StringComparer.Ordinal);
        _state = line.State;
        KeepSource(line, children);
        KeepSelection();
        Phase = RunPageState.Shown;
        if (!_opened)
        {
            _opened = true;
            OpenAtActive();
        }
    }

    // The stage the run is at: the first one in the middle of something, else the first that ended badly, else the last one reached, else the first.
    private static string? ActiveOf(RunGraph graph)
    {
        var stages = graph.Nodes.Where(n => n.Kind == RunNodeKind.Stage).ToList();
        return (stages.FirstOrDefault(n => RunStates.IsActive(n.State))
            ?? stages.FirstOrDefault(n => RunStates.EndedBadly(n.State))
            ?? stages.LastOrDefault(n => n.State != RunNodeState.NotStarted)
            ?? stages.FirstOrDefault())?.Id;
    }

    private RunCardViewModel Card(RunNode node, InstanceDetail line, List<RunNode> stages, string? active)
    {
        var rect = Layout.Rects.GetValueOrDefault(node.Id);
        var at = stages.FindIndex(n => string.Equals(n.Id, node.Id, StringComparison.Ordinal));
        var leadsTo = at >= 0 && at + 1 < stages.Count ? stages[at + 1].Name : null;
        var group = line.Children.FirstOrDefault(g => string.Equals(g.State, node.Id, StringComparison.Ordinal));
        var extra = group is { Total: > 1 } ? Labels.RunPhasesDone(group.Done, group.Total) : string.Empty;
        return new RunCardViewModel(node, rect, at + 1, stages.Count, leadsTo, string.Equals(node.Id, active, StringComparison.Ordinal), extra);
    }

    private (IReadOnlyList<MapEdge> Edges, IReadOnlyList<RunPillViewModel> Pills) EdgesOf(RunGraph graph)
    {
        var edges = new List<MapEdge>();
        var pills = new List<RunPillViewModel>();
        foreach (var edge in graph.Edges)
        {
            if (!Layout.Rects.TryGetValue(edge.From, out var from) || !Layout.Rects.TryGetValue(edge.To, out var to) || !Layout.Ports.TryGetValue(edge.Key, out var ports))
            {
                continue;
            }

            var route = EdgeRoute.Between(PortAt(from, ports.From), ports.From, PortAt(to, ports.To), ports.To);
            var (dashed, brush) = edge.Tone switch
            {
                EdgeTone.Active => (false, "Brush.PrimaryHover"),
                EdgeTone.Failed => (false, "Brush.Error"),
                EdgeTone.Dim => (true, null),
                EdgeTone.Normal => (false, null),
                _ => (false, null),
            };
            edges.Add(new MapEdge(edge.Key, route, dashed, brush));
            if (edge.Label is { Length: > 0 } label)
            {
                var middle = route.Midpoint();
                pills.Add(new RunPillViewModel(edge.Key, label, middle.X, middle.Y, edge.Tone == EdgeTone.Failed));
            }
        }

        return (edges, pills);
    }

    // The middle of a side of a card: the place an edge leaves or arrives.
    private static MapPoint PortAt(MapRect rect, PortSide side) => side switch
    {
        PortSide.Top => new MapPoint(rect.X + (rect.Width / 2), rect.Top),
        PortSide.Bottom => new MapPoint(rect.X + (rect.Width / 2), rect.Bottom),
        PortSide.Left => new MapPoint(rect.Left, rect.Y + (rect.Height / 2)),
        PortSide.Right => new MapPoint(rect.Right, rect.Y + (rect.Height / 2)),
        _ => new MapPoint(rect.Right, rect.Y + (rect.Height / 2)),
    };

    private void Header(InstanceDetail line, RunGraph graph, IReadOnlyList<InstanceSummary> runs)
    {
        Title = line.Title;
        Reference = line.Address;
        ReferenceCopy = new CopyAction(clipboard, clock, Labels.CopyAddressName(line.Address), Labels.RunCopyReference);
        var started = runs.FirstOrDefault(r => string.Equals(r.Address, line.Address, StringComparison.Ordinal))?.StartedAt ?? line.Since;
        StartedText = Labels.RunStarted(Labels.Duration((long)Math.Max(0, (clock.GetUtcNow() - started).TotalSeconds)));
        var state = graph.Nodes.FirstOrDefault(n => n.Kind == RunNodeKind.End && IsReached(line, n))?.State
            ?? graph.Nodes.FirstOrDefault(n => n.Kind == RunNodeKind.Stage && string.Equals(n.Id, ActiveNodeId, StringComparison.Ordinal))?.State
            ?? RunNodeState.NotStarted;
        var brushes = RunStates.Brushes(state);
        (HeaderState, HeaderWord, HeaderIcon, HeaderBrush, HeaderSoftBrush) = (state, RunStates.Word(state), RunStateIcons.Of(state), brushes.Text, brushes.Soft);
        Cancel = line.Actions.Contains(CancelAction, StringComparer.Ordinal) ? catalog.On(CancelAction, line.Address, send: null) : null;
    }

    // An end card was reached when the line's own state is it.
    private static bool IsReached(InstanceDetail line, RunNode end) => string.Equals(line.State, end.Id, StringComparison.Ordinal);
}
