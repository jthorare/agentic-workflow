using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Run;

#pragma warning disable MA0182 // until slice 3 places the run canvas on its page

/// <summary>
/// Builds the run canvas's graph from what is served (F3 spec 3 §4), with a fallback for everything not served yet: the engine serves no
/// transitions, so the main chain comes from the path order, a loop from the recipe's loops, only the end that was reached is drawn, and a
/// pill carries text only when the recipe served a label for that edge.
/// </summary>
internal static partial class RunGraphBuilder
{
    /// <summary>The graph of a line run.</summary>
    /// <param name="line">The line instance.</param>
    /// <param name="recipe">The line's recipe, or null when it is not read.</param>
    /// <param name="children">The stage children read in full, by instance id.</param>
    /// <returns>The graph.</returns>
    internal static RunGraph Build(InstanceDetail line, RecipeView? recipe, IReadOnlyDictionary<string, InstanceDetail> children)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(children);
        var names = recipe?.States.ToDictionary(s => s.State, s => s.DisplayName, StringComparer.Ordinal) ?? [];
        string NameOf(string id) => names.TryGetValue(id, out var served) && !string.IsNullOrWhiteSpace(served) ? served : WorkflowNames.Of(id, displayName: null);

        var stages = line.States
            .Where(c => c is { Child: true, Order: not null, BranchOf: null, Terminal: false })
            .OrderBy(c => c.Order).ThenBy(c => c.State, StringComparer.Ordinal)
            .Select(c => StageNode(c, line, children, NameOf))
            .ToList();
        var sides = line.States
            .Where(c => c is { BranchOf: not null, Terminal: false, Glyph: not Api.Workflow.StateGlyph.Pending })
            .Select(c => new RunNode(c.State, RunNodeKind.Side, NameOf(c.State), Order: null, c.BranchOf, FromGlyph(c.Glyph), Outcome: null, WorkflowName: null, ChildId: null, ChildAddress: null, Steps: []));
        var ends = line.States
            .Where(c => c is { Terminal: true, Outcome: not null })
            .Select(c => new RunNode(c.State, RunNodeKind.End, NameOf(c.State), c.Order, BranchOf: null, EndState(c.Outcome), c.Outcome, WorkflowName: null, ChildId: null, ChildAddress: null, Steps: []));
        var nodes = stages.Concat(sides).Concat(ends).ToList();
        return new RunGraph(nodes, Edges(nodes, recipe), BranchesServed: false);
    }

    private static RunNode StageNode(StateCell cell, InstanceDetail line, IReadOnlyDictionary<string, InstanceDetail> children, Func<string, string> nameOf)
    {
        var group = line.Children.FirstOrDefault(g => string.Equals(g.State, cell.State, StringComparison.Ordinal));
        var row = group is null ? null : group.Rows.FirstOrDefault(r => !IsDone(r)) ?? (group.Rows.Count > 0 ? group.Rows[^1] : null);
        var state = row is null ? FromGlyph(cell.Glyph) : RunStates.FromChip(row.Chip);
        var steps = row is not null && children.TryGetValue(row.Id, out var child) ? StepsOf(child) : [];
        var workflow = group is null ? null : WorkflowNames.Of(group.Recipe, group.DisplayName);
        return new RunNode(cell.State, RunNodeKind.Stage, nameOf(cell.State), cell.Order, BranchOf: null, state, Outcome: null, workflow, row?.Id, row?.Address, steps);
    }

    private static bool IsDone(ChildRow row) => string.Equals(row.Chip.Word, "DONE", StringComparison.OrdinalIgnoreCase);

    private static RunNodeState FromGlyph(Api.Workflow.StateGlyph glyph) => glyph switch
    {
        Api.Workflow.StateGlyph.Done => RunNodeState.Done,
        Api.Workflow.StateGlyph.Active => RunNodeState.Running,
        Api.Workflow.StateGlyph.Pending => RunNodeState.NotStarted,
        _ => RunNodeState.NotStarted,
    };

    // The way a run ended is what its end card reads: a result is Done, a failure Failed, anything unresolved did not work out, a stop is Cancelled.
    private static RunNodeState EndState(string? outcome) => outcome switch
    {
        "resolved" or "alreadySatisfied" => RunNodeState.Done,
        "failed" => RunNodeState.Failed,
        "cancelled" => RunNodeState.Cancelled,
        _ => RunNodeState.DidntWorkOut,
    };

    private static List<RunEdge> Edges(List<RunNode> nodes, RecipeView? recipe)
    {
        var edges = new List<RunEdge>();
        var stages = nodes.Where(n => n.Kind == RunNodeKind.Stage).ToList();
        AddMain(edges, stages, recipe);
        AddEnds(edges, nodes, stages, recipe);
        AddLoops(edges, nodes, recipe);
        return edges;
    }

    private static void AddMain(List<RunEdge> edges, List<RunNode> stages, RecipeView? recipe)
    {
        for (var i = 1; i < stages.Count; i++)
        {
            var (from, to) = (stages[i - 1], stages[i]);
            edges.Add(new RunEdge(from.Id, to.Id, RunEdgeKind.Main, Outcome: null, LabelFor(recipe, from.Id, to.Id), MainTone(from.State, to.State)));
        }
    }

    private static void AddEnds(List<RunEdge> edges, List<RunNode> nodes, List<RunNode> stages, RecipeView? recipe)
    {
        if (stages.Count == 0)
        {
            return;
        }

        var last = stages[^1];
        var tone = last.State == RunNodeState.Failed ? EdgeTone.Failed : EdgeTone.Normal;
        foreach (var end in nodes.Where(n => n.Kind == RunNodeKind.End))
        {
            edges.Add(new RunEdge(last.Id, end.Id, RunEdgeKind.End, end.Outcome, LabelFor(recipe, last.Id, end.Id), tone));
        }
    }

    private static void AddLoops(List<RunEdge> edges, List<RunNode> nodes, RecipeView? recipe)
    {
        foreach (var loop in recipe?.Loops ?? [])
        {
            var from = nodes.Find(n => string.Equals(n.Id, loop.From, StringComparison.Ordinal));
            var to = nodes.Find(n => string.Equals(n.Id, loop.To, StringComparison.Ordinal));
            if (from is not null && to is not null && from.State != RunNodeState.NotStarted && !edges.Exists(e => string.Equals(e.Key, from.Id + ">" + to.Id, StringComparison.Ordinal)))
            {
                edges.Add(new RunEdge(from.Id, to.Id, RunEdgeKind.Loop, Outcome: null, Labels.RunLoopPill(loop.Cap), EdgeTone.Dim));
            }
        }
    }

    // The colour rule of spec 3 §2: a done stage leading to the active one is Active; leaving one that ended badly, or reaching a stage not yet reachable, is Dim.
    private static EdgeTone MainTone(RunNodeState from, RunNodeState to)
    {
        if (from == RunNodeState.Done && RunStates.IsActive(to))
        {
            return EdgeTone.Active;
        }

        if (RunStates.EndedBadly(from))
        {
            return EdgeTone.Dim;
        }

        var fromReached = from != RunNodeState.NotStarted;
        var toReached = to != RunNodeState.NotStarted;
        return fromReached == toReached ? EdgeTone.Normal : EdgeTone.Dim;
    }

    // Only a label the recipe served is shown; none is invented.
    private static string? LabelFor(RecipeView? recipe, string from, string to) =>
        recipe?.TransitionLabels?.FirstOrDefault(t => string.Equals(t.To, to, StringComparison.Ordinal) && t.From.Contains(from, StringComparer.Ordinal))?.Label;
}
#pragma warning restore MA0182
