using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// Refusals (spec §5.5, W5, W14): what a gate answer means, and what follows a refused transition. A refusal is never
/// a dead end: every one consumes its trigger and ends in a re-run, a park, an escalation or SND-WF-004. P3 unit (a).
/// </summary>
internal static partial class RefusalRules
{
    private const string BudgetGatePrefix = "gate.budget.";

    /// <summary>
    /// What one gate's answer means (spec §5.5, W4, W5): pass; fail refuses; uncertain or below the threshold
    /// escalates for a top-rung gate and refuses otherwise; no answer (no runtime) refuses.
    /// </summary>
    /// <param name="use">The recipe's use of the gate (its threshold).</param>
    /// <param name="answer">The gate's answer; null when no runtime answered.</param>
    /// <param name="descriptor">The gate's descriptor (its rung).</param>
    /// <returns>The record the event carries.</returns>
    public static GateRecord Classify(GateUse use, GateResult? answer, BlockDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentNullException.ThrowIfNull(descriptor);
        if (answer is null)
        {
            return Unavailable(use);
        }

        var below = use.Threshold is { } threshold && (answer.Score is not { } score || score < threshold);
        var outcome = answer.Verdict switch
        {
            GateVerdict.Fail => GateOutcome.Refused,
            GateVerdict.Pass when !below => GateOutcome.Passed,
            GateVerdict.Pass or GateVerdict.Uncertain or _ => descriptor.Rung == Rung.BoundedModel ? GateOutcome.Escalated : GateOutcome.Refused,
        };
        return new GateRecord(use.Use, answer.Verdict, answer.Score, use.Threshold, answer.Reasons, outcome);
    }

    /// <summary>
    /// What follows a refused transition, in the §5.5 precedence: a 'gate.budget.*' refusal parks with budget
    /// (releasing a wait's held resources); a top-rung gate escalates; from an active state on a Step trigger the
    /// Step re-runs below the loop's cap, parks with rounds (kind refusal) at it, or parks with needs-answer without
    /// a loop; from a wait the trigger is withheld and it parks with needs-answer (kind wait); a command gets SND-WF-004.
    /// </summary>
    /// <param name="input">The refused transition and its records.</param>
    /// <returns>What follows.</returns>
    public static RefusalOutcome Refuse(RefusalInput input) => Refuse(input, EngineRules.Default);

    /// <summary><see cref="Refuse(RefusalInput)"/> through <paramref name="rules"/>.</summary>
    internal static RefusalOutcome Refuse(RefusalInput input, IEngineRules rules)
    {
        ArgumentNullException.ThrowIfNull(input);
        var refusal = new Refusal(input, Describe(input));
        if (input.Gates.FirstOrDefault(IsBudgetRefusal) is not null)
        {
            return Park(ParkReason.Budget, refusal.FromWait ? ParkKind.Wait : ParkKind.Refusal, refusal.Withhold(), refusal);
        }

        if (input.Gates.FirstOrDefault(g => g.Outcome == GateOutcome.Escalated) is { } escalated)
        {
            return Escalate(escalated, refusal);
        }

        return input.Trigger switch
        {
            StepCompleted or StepFailed when !refusal.FromWait => RefuseStep(refusal, rules),
            WaitSatisfied when refusal.FromWait => Park(ParkReason.NeedsAnswer, ParkKind.Wait, refusal.Withhold(), refusal),
            _ => new RefusalOutcome(RefusalFollow.CommandRefused, Round: null, Suspend: null,
                WorkflowErrors.GateRefused.With($"{refusal.Detail} (fix: act on the reasons, then run the command again)")),
        };
    }

    /// <summary>The Step's refusal (§5.5 step 3): a re-run below the loop's cap, a rounds or no-progress park at it, needs-answer without a loop.</summary>
    private static RefusalOutcome RefuseStep(Refusal refusal, IEngineRules rules)
    {
        var input = refusal.Input;
        var state = input.Instance.State;
        if (input.Recipe.States[state].Loop is not { } name)
        {
            return Park(ParkReason.NeedsAnswer, ParkKind.Step, Rerun(refusal), refusal);
        }

        if (rules.AtCap(input.Instance, input.Recipe, state))
        {
            return Park(ParkReason.Rounds, ParkKind.Refusal, Rerun(refusal), refusal);
        }

        var loop = input.Instance.Loops.GetValueOrDefault(name);
        if (rules.NoProgress(input.Recipe.Loops[name], loop?.Last, Mark(input)))
        {
            return Park(ParkReason.NoProgress, ParkKind.Refusal, Rerun(refusal), refusal);
        }

        return new RefusalOutcome(RefusalFollow.Rerun, (loop?.Round ?? 1) + 1, Suspend: null, Error: null);
    }

    /// <summary>A re-run that remembers the withheld transition, so status can show it and a grant re-runs the Step (never skipping a gate).</summary>
    private static ResumeAction Rerun(Refusal refusal) =>
        new(ResumeKind.Rerun, new WithheldTrigger(refusal.Input.Transition, refusal.Input.Trigger), Gate: null, Effects: []);

    private static RefusalOutcome Park(ParkReason reason, ParkKind kind, ResumeAction resume, Refusal refusal) => new(
        RefusalFollow.Park,
        Round: null,
        new Suspension(SuspensionKind.Park, reason, kind, Escalation: null, refusal.Detail, refusal.Input.Trigger.Actor, Mirrors: null, resume),
        Error: null);

    private static RefusalOutcome Escalate(GateRecord gate, Refusal refusal)
    {
        var question = $"{gate.Gate.Value} scored {Score(gate.Score)}, below {Score(gate.Threshold)} on {refusal.Move}: {Reasons(gate.Reasons)}";
        return new RefusalOutcome(
            RefusalFollow.Escalate,
            Round: null,
            new Suspension(
                SuspensionKind.Escalation, Reason: null, ParkKind: null, EscalationKind.Gate, question, refusal.Input.Trigger.Actor, Mirrors: null,
                new ResumeAction(ResumeKind.Override, new WithheldTrigger(refusal.Input.Transition, refusal.Input.Trigger), gate.Gate, Effects: [])),
            Error: null);
    }

    private static bool IsBudgetRefusal(GateRecord gate) =>
        gate.Gate.Value.StartsWith(BudgetGatePrefix, StringComparison.Ordinal) && gate.Outcome is GateOutcome.Refused or GateOutcome.Unavailable;

    internal static GateRecord Unavailable(GateUse use) =>
        new(use.Use, Verdict: null, Score: null, use.Threshold, [$"no runtime for {use.Use.Value} in this build"], GateOutcome.Unavailable);
}
