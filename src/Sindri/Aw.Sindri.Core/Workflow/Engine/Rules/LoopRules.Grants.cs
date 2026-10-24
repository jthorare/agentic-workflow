using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The answers to a loop park (spec W7, §3.8, §5.1, §5.8): the arithmetic only. The answer applies it to the parked
/// instance and then ends the park (the suspension rules), so it fits a park once; ending it counts no round, and the
/// round the resume starts counts one (§5.8). Both clear the last round's mark, so the resumed round isn't compared with the parked one again.
/// </summary>
internal static partial class LoopRules
{
    /// <summary>
    /// '--grant n' (W7): n ≥ 1 (no upper bound), on a rounds or no-progress park; the round it starts is the first of
    /// the n, so the last round allowed becomes the current round plus n. Kind loop grants the loop of the withheld
    /// transition's target; kind refusal the active state's.
    /// </summary>
    /// <param name="instance">The parked instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="rounds">n.</param>
    /// <returns>The instance with the allowed rounds raised, or SND-WF-003.</returns>
    public static Result<WorkflowInstance> Grant(WorkflowInstance instance, Recipe recipe, int rounds)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        var parked = Parked(instance, recipe, "--grant", ParkReason.Rounds, ParkReason.NoProgress);
        if (parked.IsError)
        {
            return parked.Error;
        }

        var (name, _, loop) = parked.Value;
        if (rounds < 1)
        {
            return WorkflowErrors.TriggerNotAllowed.With($"--grant {Text(rounds)} grants nothing for loop {name} (fix: use --grant 1 or more)");   // owner q93: no upper bound
        }

        return With(instance, name, loop with { Allowed = loop.Round + rounds, Last = null });
    }

    /// <summary>
    /// '--text' on a no-progress park (spec §3.8): the same as a grant (the resume performs the withheld transition or
    /// re-runs the Step) without adding rounds beyond the one it starts.
    /// </summary>
    /// <param name="instance">The parked instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <returns>The instance with the loop ready for the round the answer starts, or SND-WF-003.</returns>
    public static Result<WorkflowInstance> AnswerText(WorkflowInstance instance, Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        var parked = Parked(instance, recipe, "--text", ParkReason.NoProgress);
        if (parked.IsError)
        {
            return parked.Error;
        }

        var (name, _, loop) = parked.Value;
        var answered = loop with { Allowed = Math.Max(loop.Allowed, loop.Round + 1), Last = null, TextAnswered = true };
        return With(instance, name, answered);
    }

    /// <summary>The loop a park of one of <paramref name="reasons"/> stopped, with its counters; SND-WF-003 naming the right answer otherwise.</summary>
    private static Result<(string Name, LoopSpec Spec, LoopState Loop)> Parked(
        WorkflowInstance instance, Recipe recipe, string answer, params ReadOnlySpan<ParkReason> reasons)
    {
        if (instance.Suspension is not { Kind: SuspensionKind.Park, Reason: { } reason } park || !reasons.Contains(reason))
        {
            var fix = instance.Suspension is { Kind: SuspensionKind.Park, Reason: ParkReason.Rounds }
                ? "grant more rounds at a terminal with --grant n"
                : "use an answer from allowedCommands in status";
            return WorkflowErrors.TriggerNotAllowed.With($"{answer} doesn't answer {Shown(instance)} (fix: {fix})");
        }

        if (park.Mirrors is { } child)
        {
            return WorkflowErrors.TriggerNotAllowed.With(
                $"the park mirrors child {child.Value} (fix: answer it at this address; the answer is forwarded to the child)");
        }

        var state = LoopedState(recipe, instance.Frame, park);
        return Find(recipe, state) is var (name, spec)
            ? (name, spec, Counters(instance, name, spec))
            : WorkflowErrors.TriggerNotAllowed.With($"{state} has no loop to grant rounds to (fix: use an answer from allowedCommands in status)");
    }

    /// <summary>The looped state a park stopped: kind loop the withheld transition's target, kind refusal the active state it suspended.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="frame">The recipe frame the park is over.</param>
    /// <param name="park">The park.</param>
    /// <returns>The state whose loop a grant raises.</returns>
    internal static string LoopedState(Recipe recipe, Frame frame, Suspension park) =>
        park.ParkKind == ParkKind.Loop && park.Resume.Withheld?.Transition is int index && index >= 0 && index < recipe.Transitions.Count
            ? recipe.Transitions[index].To
            : frame.State;

    private static string Shown(WorkflowInstance instance) =>
        instance.Park is { } reason ? $"a {Parks.Name(reason)} park" : $"an instance in {instance.State}";

    private static WorkflowInstance With(WorkflowInstance instance, string name, LoopState loop) =>
        instance with { Loops = new Dictionary<string, LoopState>(instance.Loops, StringComparer.Ordinal) { [name] = loop } };
}
