using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The state band of an instance (UI screens E-20, API spec §3.8): one cell per state of the recipe in path order, built
/// from <see cref="StatePathOrder"/> so the order, the branches and the loop edges are the ones the recipe shows. Exactly
/// one cell is active (the state the instance is in, with the engine's badge when it interrupted it); states the instance entered (else, for an instance that recorded none, main-path states
/// before it) are done and the others pending; once the instance ended, its terminal state and every main-path state are done.
/// </summary>
public static class StateCells
{
    /// <summary>The cells of <paramref name="instance"/> over <paramref name="interpreted"/>'s recipe.</summary>
    /// <param name="instance">The interpreter's state.</param>
    /// <param name="interpreted">The recipe it runs and its blocks' descriptors.</param>
    /// <param name="badge">The engine's badge for the state the instance is interrupted in, if it is.</param>
    /// <param name="visits">The latest visit to each state by id, which gives each cell its enteredAt and leftAt; null leaves the cells without times.</param>
    /// <returns>The cells, in path order (<see cref="StatePathOrder"/>).</returns>
    public static IReadOnlyList<StateCell> Of(WorkflowInstance instance, InterpretedRecipe interpreted, EngineOwnedBadge? badge, IReadOnlyDictionary<string, StateVisit>? visits = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(interpreted);
        var recipe = interpreted.Recipe;
        var path = StatePathOrder.Of(recipe);
        var active = instance.Frame.State;
        var activeOrder = ActiveOrder(path, active);
        return [.. path.States.Select(place => Cell(place, new Place(instance, interpreted, path, active, activeOrder, badge, visits)))];
    }

    private static StateCell Cell(StatePlace place, Place at)
    {
        var state = at.Interpreted.Recipe.States[place.State];
        var isActive = string.Equals(place.State, at.Active, StringComparison.Ordinal);
        var visit = Visit(place.State, at);
        var terminal = state.Kind == StateKind.Terminal;
        return new StateCell(
            place.State,
            place.Order,
            place.BranchOf,
            Glyph(place, terminal, isActive, at),
            isActive && !at.Instance.Ended ? at.Badge : null,
            isActive ? Round(state, at.Instance) : null,
            Loops(place.State, at),
            Desk: state.Step is { } step && at.Interpreted.Catalog.Find(step.Use) is { DeskCapable: true },
            Child: state.WaitFor?.Child is not null,
            Children: null,   // why: child progress is engine P7's; no instance has children yet
            Outcome: Outcome(terminal && isActive, at),
            terminal,
            visit?.EnteredAt,
            visit?.LeftAt,
            state.Step?.Use.Value);
    }

    private static string? Outcome(bool endedHere, Place at) => endedHere && at.Instance.Ended ? at.Instance.Outcome?.Value : null;

    private static StateVisit? Visit(string state, Place at) => at.Visits is { } visits && visits.TryGetValue(state, out var visit) ? visit : null;

    private static StateGlyph Glyph(StatePlace place, bool terminal, bool isActive, Place at)
    {
        if (at.Instance.Ended)
        {
            return isActive || EndedDone(place, terminal, at.Instance) ? StateGlyph.Done : StateGlyph.Pending;
        }

        if (isActive)
        {
            return StateGlyph.Active;
        }

        return Passed(place, terminal, at) ? StateGlyph.Done : StateGlyph.Pending;
    }

    // An ended instance with visit data shows what it entered (its branch states too); without, every main-path state.
    private static bool EndedDone(StatePlace place, bool terminal, WorkflowInstance instance) =>
        instance.Visited is { } visited ? visited.Contains(place.State, StringComparer.Ordinal) : !terminal && place.Order is not null;

    // A state the instance entered is done (a branch state or one before a loop's re-entry too); an instance that recorded none falls back to path order.
    private static bool Passed(StatePlace place, bool terminal, Place at) =>
        at.Instance.Visited is { } visited
            ? visited.Contains(place.State, StringComparer.Ordinal)
            : !terminal && place.Order is { } order && order < at.ActiveOrder;

    /// <summary>The path position of the active state; a branch state counts as the main-path state it is folded under.</summary>
    private static int ActiveOrder(PathOrder path, string active)
    {
        var place = path.States.FirstOrDefault(p => string.Equals(p.State, active, StringComparison.Ordinal));
        var root = place?.BranchOf is { } parent ? path.States.FirstOrDefault(p => string.Equals(p.State, parent, StringComparison.Ordinal)) : place;
        return root?.Order ?? 0;
    }

    private static Round? Round(RecipeState state, WorkflowInstance instance) =>
        state.Loop is { } name && instance.Loops.TryGetValue(name, out var loop) ? new Round(loop.Round, loop.Allowed) : null;

    /// <summary>The loop pairs of a state (API spec §3.8): the other state of each named loop edge, the rounds completed so far and the recipe's cap; a self-loop pairs a state with itself.</summary>
    private static List<LoopPair> Loops(string state, Place at)
    {
        var pairs = new List<LoopPair>();
        foreach (var edge in at.Path.Loops)
        {
            if (edge.Name is not { } name || !at.Interpreted.Recipe.Loops.TryGetValue(name, out var spec))
            {
                continue;   // an unnamed loop edge has no cap to show
            }

            var with = string.Equals(edge.From, state, StringComparison.Ordinal) ? edge.To : string.Equals(edge.To, state, StringComparison.Ordinal) ? edge.From : null;
            if (with is not null)
            {
                pairs.Add(new LoopPair(with, at.Instance.Loops.TryGetValue(name, out var loop) ? Math.Max(0, loop.Round - 1) : 0, spec.Cap));
            }
        }

        return pairs;
    }

    private sealed record Place(WorkflowInstance Instance, InterpretedRecipe Interpreted, PathOrder Path, string Active, int ActiveOrder, EngineOwnedBadge? Badge, IReadOnlyDictionary<string, StateVisit>? Visits);
}
