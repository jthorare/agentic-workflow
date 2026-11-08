using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The matched transition's gates and evidence (spec §5.2, W5), and a refusal (§5.8, W14): the trigger is consumed and a
/// re-run, park, escalation or SND-WF-004 follows.
/// </summary>
public static partial class TransitionDecision
{
    /// <summary>Every gate in order and every evidence check, from the answers the gate activities returned; a missing answer had no runtime and refuses.</summary>
    private static Records Check(Deciding d, RecipeTransition transition) => new(
        [.. transition.Gates.Select(use => d.Context.Catalog.Find(use.Use) is { } descriptor
            ? RefusalRules.Classify(use, d.Input.Answers.Gates.GetValueOrDefault(use.Use), descriptor)
            : RefusalRules.Unavailable(use))],
        [.. transition.Evidence.Select(check => Evidence(check, d.Input.Answers.Evidence.GetValueOrDefault(check)))]);

    /// <summary>Evidence 'expect' (§3.5): the check holds when its result is what the recipe expected.</summary>
    private static EvidenceRecord Evidence(EvidenceCheck check, EvidenceResult? answer) => answer is null
        ? new EvidenceRecord(check.Kind, check.At, check.Expect, Held: false, [$"no runtime for evidence {check.Kind} in this build"])
        : new EvidenceRecord(check.Kind, check.At, check.Expect, answer.Passed == (check.Expect == EvidenceExpectation.Pass), answer.Reasons);

    private static Refused Refuse(Deciding d, WorkflowInstance instance, int index, Records records)
    {
        var input = new RefusalInput(instance, d.Recipe, d.Trigger, index, records.Gates, records.Evidence);
        var outcome = RefusalRules.Refuse(input, d.Rules);
        if (outcome.Follow == RefusalFollow.CommandRefused && d.Answered is null)
        {
            return new Refused(d.Key, outcome, [], [], []);
        }

        // A command refused on an answer's resume ('--reject', or '--accept' with another check refusing) still ends the
        // answer's suspension with its SND-WF-004 refusal, so the escalation never stays a dead end (§5.8, W14).
        (WorkflowInstance After, IReadOnlyList<FollowUp> FollowUps) follows = outcome.Follow switch
        {
            RefusalFollow.Rerun when StartBlocked(d, instance) is { } blocked => (d.Rules.Suspend(instance, blocked), []),
            RefusalFollow.Rerun => Rerun(d, instance, outcome.Round ?? 1, RefusalRules.Mark(input)),
            RefusalFollow.CommandRefused => (instance, []),
            RefusalFollow.Park or RefusalFollow.Escalate or _ => (d.Rules.Suspend(instance, outcome.Suspend!), []), // why: RefusalRules sets Suspend on every park and escalation
        };
        var (after, followUps) = follows;
        var suspension = outcome.Suspend;
        var change = d.Change(after, WorkflowEventType.Refused, records);
        var refusal = new RefusalRecord(outcome.Follow, suspension?.Reason, suspension?.Escalation, outcome.Round, outcome.Error?.Code);
        return new Refused(d.Key, outcome, [change with { Event = change.Event with { Refusal = refusal } }], [], followUps);
    }

    /// <summary>
    /// A re-run below the loop's cap (§5.8): a new run '{entry}.{n + 1}' in the same entry, one round, its
    /// mark kept for the next no-progress check.
    /// </summary>
    private static (WorkflowInstance After, IReadOnlyList<FollowUp> FollowUps) Rerun(Deciding d, WorkflowInstance instance, int round, ProgressMark mark)
    {
        var state = d.Recipe.States[instance.State];
        var run = instance.Frame.Run!; // why: a Step trigger matched its open run, so the frame has one
        var next = run with { Token = RunToken.For(run.Token.Entry, run.Token.Number + 1), Launch = null };
        var loops = state.Loop is { } name && instance.Loops.TryGetValue(name, out var loop)
            ? With(instance.Loops, name, loop with { Round = round, Last = mark })
            : instance.Loops;
        var after = instance with { Frame = instance.Frame with { Run = next }, Loops = loops };
        FollowUp[] start = state.Step is { } step ? [new(FollowUpKind.StartRun, instance.Id, next.Token, step.Use, Answer: null)] : [];
        return (after, start);
    }

    /// <summary>The records a transition's event carries.</summary>
    /// <param name="Gates">Each gate's record, in order.</param>
    /// <param name="Evidence">Each evidence check's record.</param>
    private sealed record Records(IReadOnlyList<GateRecord> Gates, IReadOnlyList<EvidenceRecord> Evidence)
    {
        public static Records None { get; } = new([], []);

        /// <summary>Whether a gate refused, escalated or had no runtime, or an evidence check missed its expectation.</summary>
        public bool Refused => Gates.Any(g => g.Outcome is not (GateOutcome.Passed or GateOutcome.Overridden)) || Evidence.Any(e => !e.Held);
    }
}
