using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The interpreter's state loop (engine spec rev 8 §5.1, §5.2, §5.4, §5.8; engine P4 unit (a)). Every trigger reaches
/// the instance at a wait and is decided there, one at a time, by P3's pure <see cref="TransitionDecision"/>: a command
/// an Update hands over through <see cref="Submit"/> (units (b), (c)), or a registered event the bridge delivered on the
/// 'event' signal (unit (d)) that fits the wait state's 'waitFor'. An event that fits no wait yet stays buffered until
/// one does (§5.4). An applied or refused trigger writes one ledger event for the instance (§5.6) and sets the search
/// attributes; a rejected one records nothing. Workflow code only: no I/O, clock or random outside 'Workflow.*'.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    private readonly Queue<Submission> _submitted = new();
    private readonly Dictionary<int, Decision> _decided = [];
    private int _tickets;

    /// <summary>
    /// The seam the Updates call (units (b), (c)): hands <paramref name="trigger"/> to the loop, which decides it at the
    /// instance's current wait, applies and mirrors it, and returns the decision. Its validator has already run.
    /// </summary>
    /// <param name="trigger">The command's trigger (a report, an answer, a command, a cancel …).</param>
    /// <returns>The decision: applied, refused, absorbed, or rejected (nothing recorded).</returns>
    private async Task<Decision> Submit(Trigger trigger)
    {
        var ticket = ++_tickets;
        _submitted.Enqueue(new Submission(ticket, trigger));
        await Workflow.WaitConditionAsync(() => _decided.ContainsKey(ticket)).ConfigureAwait(true);
        _ = _decided.Remove(ticket, out var decision);
        return decision ?? throw new InvalidOperationException("the loop settled the ticket without a decision");
    }

    /// <summary>
    /// Lets every Update that is still waiting on the loop finish once the instance has ended (engine P5 review): a command
    /// the Update that ended it left queued (a takeover validated beside a cancel) is decided on the ended instance, which
    /// refuses it (SND-WF-003), and a desk report that was never applied is refused; then the handlers return their results.
    /// </summary>
    private async Task SettleLeftovers(WorkflowInstance ended, Interpreting context)
    {
        while (true)
        {
            while (_submitted.TryDequeue(out var left))
            {
                _decided[left.Ticket] = TransitionDecision.Decide(new DecisionInput(ended, context.Recipe, left.Trigger, GateAnswers.None, context.Decisions));
            }

            if (_reportId is { } reportId)
            {
                _report = null;
                _reportId = null;
                Settle(reportId, new Reported(Result: null, WorkflowErrors.TriggerNotAllowed.With($"{ended.Address.Value} ended before the report was applied (fix: run status)")));
            }

            if (Workflow.AllHandlersFinished)
            {
                return;
            }

            await Workflow.WaitConditionAsync(() => Workflow.AllHandlersFinished || _submitted.Count > 0 || _reportId is not null).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Waits for the next trigger and decides it (§5.4, §5.5, §5.8): in a wait state, the first delivered event that fits
    /// its 'waitFor', a submitted command, or its timer running out, whichever is first; parked, escalated or taken over,
    /// a submitted command (a park stops the timer). A child wait is P7's: the child isn't started yet, so only its
    /// delivered end, a command or its timer moves it.
    /// </summary>
    private async Task<WorkflowInstance> Wait(WorkflowInstance instance, Interpreting context)
    {
        var wait = instance.Suspension is null && context.Recipe.States.TryGetValue(instance.Frame.State, out var state) ? state.WaitFor : null;
        if (!await WaitHolding(() => _submitted.Count > 0 || _timers.Fired || _delivered.Any(e => Fits(e, wait, instance)), instance, wait).ConfigureAwait(true))
        {
            return instance;   // woke only to expire a held event (§5.4); wait again
        }

        if (_submitted.TryDequeue(out var submission))
        {
            return await Apply(instance, submission, context).ConfigureAwait(true);
        }

        Trigger next = _delivered.Any(e => Fits(e, wait, instance)) ? Take(wait, instance) : _timers.Take()!; // why: WaitHolding woke for an event that fits or a fired timer
        return await Apply(instance, new Submission(Ticket: 0, next), context).ConfigureAwait(true);
    }

    /// <summary>
    /// Decides one trigger and applies its change (§5.1, §5.6): the instance after it, one ledger event through
    /// 'RecordEvent', the search attributes; the decision goes back to the Update that submitted it.
    /// </summary>
    private async Task<WorkflowInstance> Apply(WorkflowInstance instance, Submission submission, Interpreting context)
    {
        var decision = TransitionDecision.Decide(await Deciding(instance, context, submission.Trigger).ConfigureAwait(true));
        var after = After(instance, decision);
        if (OwnChange(instance, decision) is { } change)
        {
            after = Enter(after, submission.Trigger.Actor);
            await ReadRecommendation(after).ConfigureAwait(true);
            await Mirror(Record(instance, change, submission.Trigger), NeedsYou(after)).ConfigureAwait(true);
            Emit(instance, after, EffectsOf(decision), change.Event.Type);   // a takeoverIdle row (P5) is an engine notice
            Spawn(decision);
            Retry();   // a child's change held while the parent was busy gets another try
        }
        else
        {
            Hold(submission, decision);
        }

        if (submission.Ticket != 0)
        {
            _decided[submission.Ticket] = decision;
        }

        return after;
    }

    /// <summary>The first delivered event that fits the wait, taken out of the buffer; the others stay, in order.</summary>
    private WaitSatisfied Take(WaitFor? wait, WorkflowInstance instance)
    {
        WaitSatisfied? taken = null;
        for (var left = _delivered.Count; left > 0; left--)
        {
            var delivered = _delivered.Dequeue();
            if (taken is null && Fits(delivered, wait, instance))
            {
                taken = delivered;
            }
            else
            {
                _delivered.Enqueue(delivered);
            }
        }

        return taken ?? throw new InvalidOperationException("no delivered event fits the wait the loop woke for");
    }

    /// <summary>Whether <paramref name="delivered"/> is what the wait waits for: its event kind, its resource, or its child's end.</summary>
    private static bool Fits(WaitSatisfied delivered, WaitFor? wait, WorkflowInstance instance) =>
        (delivered.Source is ChildWaitSource child && instance.Suspension?.Mirrors == child.Child) || Fits(delivered, wait, instance.Frame);   // a child's end also ends the mirror of its own park (§5.8)

    private static bool Fits(WaitSatisfied delivered, WaitFor? wait, Frame frame) => (wait, delivered.Source) switch
    {
        (null, _) => false,
        (_, EventWaitSource e) => wait.Event == e.Kind,
        (_, ResourceWaitSource r) => wait.Resource == r.Resource,
        (_, ChildWaitSource c) => wait.Child is not null && frame.Child == c.Child,
        _ => false,
    };

    /// <summary>The decision's change to this instance (an applied or refused trigger); none for a rejected or absorbed one.</summary>
    private static InstanceChange? OwnChange(WorkflowInstance instance, Decision decision) => decision switch
    {
        Applied applied => applied.Changes.FirstOrDefault(c => c.After.Id == instance.Id),
        Refused refused => refused.Changes.FirstOrDefault(c => c.After.Id == instance.Id),
        _ => null,
    };

    /// <summary>The ledger row for <paramref name="change"/>: its event type, the states, the trigger, its outcome and the run it reported on.</summary>
    private WorkflowEventRecord Record(WorkflowInstance before, InstanceChange change, Trigger trigger) => new(
        _start!.WorkflowId().Value, // why: the loop runs only after RunAsync set _start
        TypeOf(change.Event.Type),
        _start.Item.Value,
        before.State,
        change.After.State,
        trigger.Name,
        trigger switch
        {
            StepCompleted completed => completed.Outcome.Value,
            WaitSatisfied satisfied => satisfied.Outcome?.Value,
            _ => null,
        },
        before.Frame.Run?.Token.Value,
        trigger.Actor,
        Detail: Located(WorkflowEventDetail.Of(change.Event, trigger).WithRunOpening(change.After, _context!.Recipe), change.After),   // why: a decision is recorded only after Begin set the context
        Causation: (trigger as OpenRequested)?.Launch);

    internal static string TypeOf(WorkflowEventType type) => type switch
    {
        WorkflowEventType.Started => EventTypes.WorkflowStarted,
        WorkflowEventType.Refused => EventTypes.WorkflowRefused,
        WorkflowEventType.Shifted => EventTypes.WorkflowShifted,
        WorkflowEventType.Notified => EventTypes.WorkflowNotified,
        WorkflowEventType.Expired => EventTypes.WorkflowExpired,
        WorkflowEventType.Heavy => EventTypes.WorkflowHeavy,

        // why: spec 5.7 names a dispatch refusal 'outbox.refused', written by P6's recheck; P4 runs no effects, so there is no kind to emit yet
        WorkflowEventType.EffectRefused => throw new NotSupportedException("an effect refusal is mirrored from P6 (outbox.refused)"),
        WorkflowEventType.Transition or _ => EventTypes.WorkflowTransition,
    };

    /// <summary>
    /// Sets the search attributes (spec §5.1) as they stand: 'Item', 'Recipe', 'State', 'NeedsYou', 'Tenant', 'Ring' and
    /// 'Root' from the input and the instance, 'Urgency' from the recipe, 'Subject' (the key <see cref="SubjectRefs"/> names from the first declared input's value),
    /// and 'Repo' when 'fact:item' named one.
    /// </summary>
    private void Upsert(string state, bool needsYou)
    {
        var start = _start!; // why: Mirror runs only after RunAsync set _start
        List<SearchAttributeUpdate> updates =
        [
            SindriTemporal.ItemKey.ValueSet(start.Item.Value),
            SindriTemporal.RecipeKey.ValueSet(start.Recipe.Id.Value),
            SindriTemporal.RecipeHashKey.ValueSet(start.Recipe.Hash.Value),
            SindriTemporal.StateKey.ValueSet(state),
            SindriTemporal.NeedsYouKey.ValueSet(needsYou),
            SindriTemporal.TenantKey.ValueSet(start.Tenant),
            SindriTemporal.RingKey.ValueSet(start.Ring.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            SindriTemporal.RootKey.ValueSet(Workflow.Info.Parent is null && !(Workflow.Patched(LinkedEventsPatch) && start.WorkflowId().LinkedParent() is not null)),   // a linked instance (LN-WF1) is no root
        ];
        if (_recipe is { } recipe)
        {
            updates.Add(SindriTemporal.UrgencyKey.ValueSet(Urgencies.Word(RecipeUrgencies.UrgencyOf(recipe))));
            if (_subject is { Key.Length: > 0 } subject)
            {
                updates.Add(SindriTemporal.SubjectKey.ValueSet(subject.Key));
            }
        }

        if (_repo is { } repo)
        {
            updates.Add(SindriTemporal.RepoKey.ValueSet(repo));
        }

        Workflow.UpsertTypedSearchAttributes([.. updates]);
    }

    /// <summary>A stored input's content as text ('ReadSubject', a local activity on start: one call per fact the name reads, the first input, and each link input); none when it can't be read.</summary>
    private static async Task<string?> SubjectTextOf(FactValue fact)
    {
        try
        {
            return await Workflow.ExecuteLocalActivityAsync(
                (FactReadActivities a) => a.ReadSubjectAsync(fact),
                new LocalActivityOptions { StartToCloseTimeout = ActivityBounds.Long, RetryPolicy = new RetryPolicy { MaximumAttempts = 3 } }).ConfigureAwait(true);
        }
        catch (Exception failed) when (IsActivityFailure(failed))
        {
            return null; // boundary: content that can't be read leaves the subject named by its fact key; the start goes on
        }
    }

    /// <summary>The repo 'fact:item' names ('ReadItemRepo', a local activity on start); none when it names none, and Unreadable when it can't be read (a block Step that needs the repo then fails instead of using the box's list).</summary>
    private static async Task<(string? Repo, bool Unreadable)> RepoOf(FactValue item)
    {
        try
        {
            var repo = await Workflow.ExecuteLocalActivityAsync(
                (FactReadActivities a) => a.ReadRepoAsync(item),
                new LocalActivityOptions { StartToCloseTimeout = ActivityBounds.Long, RetryPolicy = new RetryPolicy { MaximumAttempts = 3 } }).ConfigureAwait(true);
            return (repo, false);
        }
        catch (Exception failed) when (IsActivityFailure(failed))
        {
            return (null, true); // boundary: a fact:item that can't be read leaves 'Repo' unset (spec §5.1); the start goes on
        }
    }

    /// <summary>
    /// Whether <paramref name="failure"/> is a failure of the local activity: it ran out of attempts (the SDK raises that as the activity's own
    /// <see cref="ApplicationFailureException"/>), timed out (<see cref="TimeoutFailureException"/>), or was an ordinary activity's <see cref="ActivityFailureException"/>.
    /// A cancellation is none of these, so it still ends the workflow.
    /// </summary>
    internal static bool IsActivityFailure(Exception failure) => failure is FailureException and not CanceledFailureException;
}
