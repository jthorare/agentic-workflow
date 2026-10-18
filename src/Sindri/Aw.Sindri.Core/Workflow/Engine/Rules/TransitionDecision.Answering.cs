using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// 'workflow answer' (spec §5.8, §3.8, W7): the answer must fit the suspension (AnswerFit), a grant is applied to the
/// parked instance (LoopRules.Grant, LoopRules.AnswerText, BudgetRules.GrantTokens), the suspension ends, and its
/// resume action runs under the answer's own key. A pending park takes effect instead.
/// </summary>
public static partial class TransitionDecision
{
    private static readonly FactKey _answer = FactKey.From("fact:answer");

    private static Decision Answer(Deciding d, WorkflowInstance instance, AnswerGiven answer)
    {
        var fit = AnswerFit.Check(instance, d.Recipe, answer);
        if (fit.IsError)
        {
            return new Rejected(d.Key, fit.Error);
        }

        if (fit.Value.Kind == ResumeKind.Forward)
        {
            return new Absorbed(d.Key, $"the answer goes to the child the park mirrors, {instance.Suspension?.Mirrors?.Value} (spec 5.8)");
        }

        var granted = Grant(d, instance, answer);
        if (granted.IsError)
        {
            return new Rejected(d.Key, granted.Error);
        }

        var ending = d.Rules.End(WithAnswer(granted.Value, answer), child: null);
        if (ending.IsError)
        {
            return new Rejected(d.Key, ending.Error);
        }

        var ended = ending.Value;
        var resumed = ended.PendingTookEffect ? d.Changed(ended.Instance) : Resume(d, ended.Instance, fit.Value, neverRan: instance.Suspension?.Sandbox is not null);
        // The grant was applied before the suspension ended, so both branches record it; an override exists only when the resume ran (§5.8).
        return Recorded(WithReleased(resumed, ended.Released), answer, ended.PendingTookEffect ? fit.Value with { Gate = null } : fit.Value);
    }

    /// <summary>The answer's own event carries the grant or the override it made (§5.6).</summary>
    private static Decision Recorded(Decision decision, AnswerGiven answer, ResumeAction resume)
    {
        var grant = answer.Variant switch
        {
            AnswerVariant.Grant => new GrantRecord(answer.Amount, Tokens: null),
            AnswerVariant.GrantTokens => new GrantRecord(Rounds: null, answer.Amount),
            AnswerVariant.Text or AnswerVariant.Accept or AnswerVariant.Reject or AnswerVariant.Resume or _ => null,
        };
        var overridden = answer.Variant == AnswerVariant.Accept && resume.Gate is { } gate ? new OverrideRecord(gate) : null;
        if (grant is null && overridden is null)
        {
            return decision;
        }

        IReadOnlyList<InstanceChange> Mark(IReadOnlyList<InstanceChange> changes) =>
            changes.Count == 0 ? changes : [changes[0] with { Event = changes[0].Event with { Grant = grant, Override = overridden } }, .. changes.Skip(1)];

        return decision switch
        {
            Applied applied => applied with { Changes = Mark(applied.Changes) },
            Refused refused => refused with { Changes = Mark(refused.Changes) },
            _ => decision,
        };
    }

    /// <summary>A grant raises the loop's or the budget's allowance on the parked instance, before the park ends (W7); '--text' on no-progress works once per loop.</summary>
    private static Result<WorkflowInstance> Grant(Deciding d, WorkflowInstance instance, AnswerGiven answer) => answer.Variant switch
    {
        AnswerVariant.Grant => LoopRules.Grant(instance, d.Recipe, answer.Amount ?? 0),
        AnswerVariant.GrantTokens => BudgetRules.GrantTokens(instance, d.Recipe, answer.Amount ?? 0),
        AnswerVariant.Text when instance.Park == ParkReason.NoProgress => LoopRules.AnswerText(instance, d.Recipe),
        AnswerVariant.Text or AnswerVariant.Accept or AnswerVariant.Reject or AnswerVariant.Resume or _ => instance,
    };

    /// <summary>'fact:answer' holds the latest answer text (§5.8), as a reference to its digest.</summary>
    private static WorkflowInstance WithAnswer(WorkflowInstance instance, AnswerGiven answer)
    {
        if (answer.Text is not { } text)
        {
            return instance;
        }

        return instance with { Facts = new Dictionary<FactKey, FactValue>(instance.Facts) { [_answer] = AnswerFact(text) } };
    }

    private static FactValue AnswerFact(string text)
    {
        var digest = RefusalRules.Digest(text);
        return new FactValue("blob:" + digest, digest, HeadSha: null);
    }

    /// <summary>The ended suspension's resume action (§5.8 table).</summary>
    private static Decision Resume(Deciding d, WorkflowInstance instance, ResumeAction resume, bool neverRan = false)
    {
        var withheld = resume.Withheld;
        return resume.Kind switch
        {
            ResumeKind.Transition when withheld?.Transition is { } index => Apply(d.Withholding(withheld.Trigger), instance, index, Records.None),
            ResumeKind.Reevaluate or ResumeKind.Override or ResumeKind.Refuse when withheld?.Transition is { } index =>
                Reevaluate(d.Withholding(withheld.Trigger), instance, index, resume),
            ResumeKind.Rerun => RerunAfterAnswer(d, instance, resume, counts: !neverRan),   // a refused launch ran no round of the Step
            ResumeKind.Restart when instance.Frame.Run is not null => RerunAfterAnswer(d, instance, resume),
            ResumeKind.Restart => Restart(d, instance),
            ResumeKind.None => new Rejected(d.Key, WorkflowErrors.TriggerNotAllowed.With(
                $"nothing resumes {instance.Address.Value} (fix: cancel it with sindri workflow cancel {instance.Address.Value})")),
            ResumeKind.Reemit or ResumeKind.Rearm when resume.Effects.Count > 0 => Reemit(d, instance, resume),   // a drift approval re-sends the effects refused for the drift
            ResumeKind.Transition or ResumeKind.Reevaluate or ResumeKind.Override or ResumeKind.Refuse or ResumeKind.Rearm
                or ResumeKind.Rewait or ResumeKind.Restart or ResumeKind.Return or ResumeKind.Forward or _ => d.Changed(instance),
        };
    }

    /// <summary>The withheld transition again: every gate and evidence check from the answer's gate answers; '--accept' overrides the escalated gate, '--reject' lets its refusal stand.</summary>
    private static Decision Reevaluate(Deciding d, WorkflowInstance instance, int index, ResumeAction resume)
    {
        var records = Check(d, d.Recipe.Transitions[index]);
        if (resume.Gate is { } gate && resume.Kind is ResumeKind.Override or ResumeKind.Refuse)
        {
            var outcome = resume.Kind == ResumeKind.Override ? GateOutcome.Overridden : GateOutcome.Refused;
            records = records with { Gates = [.. records.Gates.Select(g => g.Gate == gate ? g with { Outcome = outcome } : g)] };
        }

        return records.Refused ? Refuse(d, instance, index, records) : Apply(d, instance, index, records);
    }

    /// <summary>
    /// A new run of the Step in the same entry (§5.8 rerun, restart). A rerun counts a round of the state's loop, so at
    /// the loop's allowed rounds it parks with rounds (kind refusal, the withheld trigger kept) instead of running:
    /// '--grant n' is the only way past the cap (§3.7, §3.8, W7).
    /// </summary>
    private static Applied RerunAfterAnswer(Deciding d, WorkflowInstance instance, ResumeAction resume, bool counts = true)
    {
        var state = d.Recipe.States[instance.State];
        if (instance.Frame.Run is not { } run || state.Step is not { } step)
        {
            return d.Changed(instance);
        }

        var countsRound = counts && resume.Kind == ResumeKind.Rerun;
        if (countsRound && state.Loop is { } capped && d.Rules.AtCap(instance, d.Recipe, instance.State))
        {
            return d.Changed(d.Rules.Suspend(instance, AtCapPark(capped, step.Use, resume, d.Trigger.Actor)));
        }

        var next = run with { Token = RunToken.For(run.Token.Entry, run.Token.Number + 1), Launch = null };
        var loops = countsRound && state.Loop is { } name && instance.Loops.TryGetValue(name, out var loop)
            ? With(instance.Loops, name, loop with { Round = loop.Round + 1 })
            : instance.Loops;
        var after = instance with { Frame = instance.Frame with { Run = next }, Loops = loops };
        var applied = d.Changed(after);
        return applied with { FollowUps = [new FollowUp(FollowUpKind.StartRun, instance.Id, next.Token, step.Use, Answer: null)] };
    }

    private static Suspension AtCapPark(string loop, BlockId step, ResumeAction resume, string actor) => new(
        SuspensionKind.Park, ParkReason.Rounds, ParkKind.Refusal, Escalation: null,
        $"{loop} used all its rounds; {step.Value} runs again only with more rounds", actor, Mirrors: null,
        new ResumeAction(ResumeKind.Rerun, resume.Withheld, Gate: null, Effects: []));

    /// <summary>
    /// An SND-WF-010 park's restart (§5.8): its frame never opened a run, so the Step's run opens now, as a real entry
    /// would, and starts once the decision is in history; with no runtime still, it parks with SND-WF-010 again.
    /// </summary>
    private static Applied Restart(Deciding d, WorkflowInstance instance)
    {
        if (d.Recipe.States[instance.State].Step is not { } step)
        {
            return d.Changed(instance);
        }

        if (StateEntry.Restart(instance, d.Recipe, d.Context) is not { Frame.Run: { } run } restarted)
        {
            return d.Changed(d.Rules.Suspend(instance, NoRuntime(step.Use, d.Trigger.Actor)));
        }

        return d.Changed(restarted) with { FollowUps = [new FollowUp(FollowUpKind.StartRun, instance.Id, run.Token, step.Use, Answer: null)] };
    }

    private sealed partial record Deciding
    {
        /// <summary>The answer a resume is decided under, recorded as the event's trigger while the withheld trigger is replayed.</summary>
        public AnswerGiven? Answered { get; init; }

        /// <summary>This decision replaying <paramref name="withheld"/> under the answer's key.</summary>
        public Deciding Withholding(Trigger withheld) => this with
        {
            Input = Input with { Trigger = withheld },
            Answered = Answered ?? Input.Trigger as AnswerGiven,
        };
    }
}
