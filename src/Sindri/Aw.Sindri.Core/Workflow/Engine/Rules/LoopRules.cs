using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// Loops and round grants (spec §3.7, §5.5, §5.8, W7, A §17.23.13): rounds counted on the transition back into a
/// looped state, no-progress between rounds, and '--grant n'. P3 unit (c).
/// </summary>
/// <remarks>
/// No-progress until P8 (<see cref="NoProgress"/>, spec §3.7, owner decision rev 7.2): identical findings and a repeated
/// failure reason only. The agent runtime (P8) adds finding keys and a diff signal to Step reports, and with them the
/// findings superset and the empty diff of A §17.23.13.
/// A round is counted on the transition back into the looped state (<see cref="Reenter"/>) and on a re-run after a
/// refusal (the decision bumps it, §5.5); ending a suspension never counts one (§5.8). A park this class decides carries
/// no actor and no withheld trigger: the decision that matched the trigger sets both.
/// </remarks>
internal static partial class LoopRules
{
    /// <summary>
    /// Entering a looped state again (spec §3.7): the next round, or a park with no-progress (the round repeats the
    /// last one's mark on a signal the loop names) or rounds (past the allowed rounds), kind loop, resume transition.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="state">The looped state being entered again.</param>
    /// <param name="mark">What the round that ends produced.</param>
    /// <returns>Go on, or park.</returns>
    public static LoopVerdict Reenter(WorkflowInstance instance, Recipe recipe, string state, ProgressMark mark)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(mark);
        if (Find(recipe, state) is not var (name, spec))
        {
            return new LoopVerdict(Next: null, Park: null);
        }

        var loop = Counters(instance, name, spec);
        if (Signal(spec, loop.Last, mark) is { } signal)
        {
            var question = $"round {Text(loop.Round)} of {name} made no progress over round {Text(loop.Round - 1)} ({Describe(signal)})";
            return new LoopVerdict(Next: null, LoopPark(ParkReason.NoProgress, question));
        }

        return loop.Round >= loop.Allowed
            ? new LoopVerdict(Next: null, LoopPark(ParkReason.Rounds, $"{name} used all {Text(loop.Allowed)} rounds"))
            : new LoopVerdict(loop with { Round = loop.Round + 1, Last = mark }, Park: null);
    }

    /// <summary>Whether the loop of <paramref name="state"/> is at its allowed rounds (a refusal there parks with rounds, kind refusal).</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="state">The active state.</param>
    /// <returns>True at the cap; false below it or without a loop.</returns>
    public static bool AtCap(WorkflowInstance instance, Recipe recipe, string state)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        if (Find(recipe, state) is not var (name, spec))
        {
            return false;
        }

        var loop = Counters(instance, name, spec);
        return loop.Round >= loop.Allowed;
    }

    /// <summary>
    /// Whether <paramref name="current"/> made no progress over <paramref name="previous"/> on a signal <paramref name="loop"/>
    /// names, as P3–P7 detect it (spec §3.7; the full A §17.23.13 rule lands with P8). Findings are compared by the digest
    /// of the whole 'fact:findings' value, so they count as the same only when they are identical; a superset of the last
    /// round's isn't detected, because the engine sees no finding keys. The same failure is a 'step.failed', or a gate
    /// refusal, with the same reason. An empty diff never fires: a Step report carries no diff signal until P8, so
    /// <see cref="ProgressMark.EmptyDiff"/> is always false (docs/csharp/sindri-workflow.md).
    /// </summary>
    /// <param name="loop">The loop.</param>
    /// <param name="previous">The last round's mark; null for the first round.</param>
    /// <param name="current">This round's mark.</param>
    /// <returns>True when a named signal repeats.</returns>
    public static bool NoProgress(LoopSpec loop, ProgressMark? previous, ProgressMark current)
    {
        ArgumentNullException.ThrowIfNull(loop);
        ArgumentNullException.ThrowIfNull(current);
        return Signal(loop, previous, current) is not null;
    }

    /// <summary>The 'next' variables of every loop the recipe names (spec §3.7): '{loop.&lt;n&gt;}' (the round) and '{loop.&lt;n&gt;.cap}' (the last round allowed).</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <returns>The values, by variable name without braces.</returns>
    public static IReadOnlyDictionary<string, string> Values(WorkflowInstance instance, Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, spec) in recipe.Loops)
        {
            var loop = instance.Loops.GetValueOrDefault(name);
            values["loop." + name] = Text(loop?.Round ?? 0);
            values["loop." + name + ".cap"] = Text(loop?.Allowed ?? spec.Cap);
        }

        return values;
    }

    /// <summary>The loop on <paramref name="state"/>, if the state exists and names one the recipe declares.</summary>
    private static (string Name, LoopSpec Spec)? Find(Recipe recipe, string state) =>
        recipe.States.TryGetValue(state, out var target) && target.Loop is { } name && recipe.Loops.TryGetValue(name, out var spec)
            ? (name, spec)
            : null;

    /// <summary>The loop's counters; before the first entry recorded them, round 1 of the cap (§3.7).</summary>
    private static LoopState Counters(WorkflowInstance instance, string name, LoopSpec spec) =>
        instance.Loops.GetValueOrDefault(name) ?? new LoopState(1, spec.Cap, Last: null);

    /// <summary>The first signal the loop names on which <paramref name="current"/> repeats <paramref name="previous"/>; round 1 has nothing to compare with.</summary>
    private static NoProgressSignal? Signal(LoopSpec loop, ProgressMark? previous, ProgressMark current)
    {
        if (previous is null)
        {
            return null;
        }

        foreach (var signal in loop.NoProgress)
        {
            var repeats = signal switch
            {
                NoProgressSignal.Findings => Same(previous.Findings, current.Findings),
                NoProgressSignal.EmptyDiff => current.EmptyDiff,
                NoProgressSignal.SameFailure => Same(previous.Failure, current.Failure),
                _ => false,
            };
            if (repeats)
            {
                return signal;
            }
        }

        return null;
    }

    private static bool Same(string? previous, string? current) =>
        previous is not null && string.Equals(previous, current, StringComparison.Ordinal);

    private static string Describe(NoProgressSignal signal) => signal switch
    {
        NoProgressSignal.Findings => "the same findings twice",
        NoProgressSignal.EmptyDiff => "an empty diff",
        NoProgressSignal.SameFailure or _ => "the same failure twice",
    };

    /// <summary>A loop park (kind loop): its grant performs the withheld transition back into the looped state (§5.8).</summary>
    private static Suspension LoopPark(ParkReason reason, string question) => new(
        SuspensionKind.Park, reason, ParkKind.Loop, Escalation: null, question, Actor: string.Empty, Mirrors: null,
        new ResumeAction(ResumeKind.Transition, Withheld: null, Gate: null, Effects: []));

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}
