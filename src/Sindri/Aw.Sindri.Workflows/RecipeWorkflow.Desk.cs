using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The desk runtime of <see cref="RecipeWorkflow"/> (engine spec rev 8.1 §5.3, W20; engine P4 unit (c)): 'RunStep'
/// records the open task, the workflow waits for the 'complete' Update bounded by the desk timeout, the validator
/// refuses a stale run token or a malformed report without touching state or history, and the timeout parks.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    /// <summary>
    /// 'workflow complete' from the desk (spec rev 8.1 §5.3): the report for the open run, which the desk wait consumes.
    /// Update id: the nonce. Unit (c).
    /// </summary>
    /// <param name="report">The report: the run token, a declared outcome and the stored outputs.</param>
    /// <returns>The instance after the report, and what happened ('-> checks').</returns>
    [WorkflowUpdate("complete")]
    public async Task<CommandResult> CompleteAsync(StepCompleted report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var id = Workflow.CurrentUpdateInfo?.Id ?? throw new InvalidOperationException("'complete' runs only as an Update");
        _report = report;
        _reportId = id;
        await Workflow.WaitConditionAsync(() => _reported.ContainsKey(id)).ConfigureAwait(true);
        _ = _reported.Remove(id, out var reported);
        return reported?.Result ?? throw Refusal(reported?.Refusal ?? WorkflowErrors.TriggerNotAllowed.With("the report was dropped"));
    }

    /// <summary>
    /// Rejects, recording nothing and consuming no round (rev 8.1 §5.3, W12): a run token that isn't the open run's, or a
    /// run already reported and being applied (SND-WF-005); a malformed report (SND-WF-008); a report the current state
    /// doesn't take (SND-WF-003). It runs the same pure decision the loop runs, so what it accepts the loop applies. Unit (c).
    /// </summary>
    /// <param name="report">The report.</param>
    [WorkflowUpdateValidator(nameof(CompleteAsync))]
    public void ValidateComplete(StepCompleted report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (_instance is not { } instance || _context is not { } context)
        {
            throw Refusal(WorkflowErrors.TriggerNotAllowed.With($"{_start?.WorkflowId().Value} is still starting (fix: run status, then complete the run it shows)"));
        }

        if (_report is not null)
        {
            throw Refusal(WorkflowErrors.StaleRunToken.With($"run {_report.Run.Value} was already reported and is being applied (fix: use the token in status)"));
        }

        if (instance.Suspension is { } suspension)
        {
            // why: the parked loop never reads a report, so one accepted here would hang its Update and apply stale after the resume
            throw Refusal(WorkflowErrors.TriggerNotAllowed.With(ParkedRefusal(instance, suspension, report.Run)));
        }

        if (TransitionDecision.Decide(new DecisionInput(instance, context.Recipe, report, GateAnswers.None, context.Decisions)) is Rejected rejected)
        {
            throw Refusal(rejected.Error);
        }
    }

    // Precise only where it is certain. A takeover with no unapproved park queued ends with the handback (EndCurrent skips approved
    // parks): a block run's Step then runs again, and a desk run completes with its unchanged token. Past that point the way out
    // depends on the park (its answer, and whether the Step runs again), so the text points at 'status', which shows the answer.
    internal static string ParkedRefusal(WorkflowInstance instance, Suspension suspension, RunToken run)
    {
        var address = instance.Address.Value;
        var parked = $"{address} is parked ({suspension.Reason?.ToString() ?? suspension.Kind.ToString()}); ";
        if (suspension.Kind != SuspensionKind.Takeover)
        {
            return $"{parked}answer it first: sindri workflow status {address} shows the answer it takes";
        }

        var handback = $"{parked}hand it back first: sindri workflow handback {address}";
        if (instance.PendingPark is { Approved: false } || instance.LaterParks?.Any(static p => !p.Approved) == true)
        {
            return $"{handback}; a park follows, and sindri workflow status {address} shows the answer it takes";
        }

        return instance.Frame.Run is { Runtime: StepRuntime.Block }
            ? $"{handback} (the handback runs the Step again)"
            : $"{handback}, then complete run {run.Value}";
    }

    /// <summary>
    /// One pass over an active state's desk Step: wait for its report or its timeout, decide, mirror a change into the
    /// ledger, and hand the outcome to the 'complete' Update that carried the report.
    /// </summary>
    private async Task<WorkflowInstance> RunDeskStep(WorkflowInstance instance, OpenRun open, Interpreting context)
    {
        if (await RunStep(instance, open, context.Recipe.States[instance.Frame.State]).ConfigureAwait(true) is not { } trigger)
        {
            return await ApplySubmitted(instance, context).ConfigureAwait(true);
        }

        var reportId = _reportId;
        _reportId = null;
        var before = instance;
        var decision = TransitionDecision.Decide(new DecisionInput(instance, context.Recipe, trigger, GateAnswers.None, context.Decisions));   // a desk timer's expiry always parks (W20)
        if (decision is Rejected rejected)
        {
            Settle(reportId, new Reported(Result: null, rejected.Error));   // the validator saw the same instance, so this is a bug guard
            return instance;
        }

        instance = After(instance, decision);
        if (!ReferenceEquals(before, instance))
        {
            instance = Enter(instance, decision is null ? Timers : trigger.Actor);
            await Mirror(DeskRow(before, instance, trigger, open, decision), NeedsYou(instance)).ConfigureAwait(true);
            Emit(before, instance, EffectsOf(decision));
        }

        Settle(reportId, new Reported(new CommandResult(GetInstance(), Happened(before, instance, decision)), Refusal: null));
        return instance;
    }

    /// <summary>
    /// Applies an answer, command or cancel another Update submitted while the desk task is open (unit (a)'s
    /// <see cref="Submit"/>); its change re-arms or stops the timer like any other (<see cref="Enter"/>).
    /// </summary>
    private Task<WorkflowInstance> ApplySubmitted(WorkflowInstance instance, Interpreting context) =>
        Apply(instance, _submitted.Dequeue(), context);

    /// <summary>The report's row comes from its decision, as the loop's rows do; the deadline's park has none.</summary>
    private WorkflowEventRecord DeskRow(WorkflowInstance before, WorkflowInstance after, Trigger trigger, OpenRun open, Decision? decision) =>
        decision is not null && OwnChange(before, decision) is { } change
            ? Record(before, change, trigger)
            : Transition(_start!, before, after, trigger, open);   // why: the loop runs only after RunAsync set _start

    /// <summary>The 'workflow.transition' row for the desk timeout's park (a report's row comes from its decision).</summary>
    private static WorkflowEventRecord Transition(InstanceStart start, WorkflowInstance before, WorkflowInstance after, Trigger trigger, OpenRun open)
    {
        var (kind, outcome) = trigger switch
        {
            StepCompleted c => (TriggerNames.StepCompleted, c.Outcome.Value),
            StepFailed => (TriggerNames.StepFailed, null),
            _ => (TriggerNames.Deadline, null),
        };
        return new WorkflowEventRecord(start.WorkflowId().Value, EventTypes.WorkflowTransition, start.Item.Value, before.State, after.State, kind, outcome, open.Token.Value, trigger.Actor,
            Detail: WorkflowEventDetail.Of(trigger, after.Id, after.Suspension));
    }

    /// <summary>
    /// Runs one desk Step (rev 8.1 §5.3): once per run, the short 'RunStep' activity with the run token in its id
    /// ('run:{token}:{n}') records the open task. The workflow then waits for the 'complete' Update's report, bounded by
    /// the state's timer (<see cref="TimerScopes"/>), which the entry armed: the state's timeout raised to
    /// 'workflow.deskTimeout' (W20); its expiry is the desk timeout, which always parks (W20, §3.8). A later wait on the
    /// same run keeps the timer. Unit (c). The wait also wakes for an answer, command or cancel another Update
    /// submitted; it then returns null and the loop applies that command (unit (b)).
    /// </summary>
    private async Task<Trigger?> RunStep(WorkflowInstance instance, OpenRun open, RecipeState state)
    {
        await RecordRun(instance.Frame, open, state).ConfigureAwait(true);
        while (true)
        {
            await ExpireHeld(instance, wait: null).ConfigureAwait(true);   // an active state waits for no event: what ran out while the Step is open expires on time (§5.4)
            if (_report is { } report)
            {
                _report = null;   // a report that lands with the timer wins it, and none is left stale
                return report;
            }

            if (_submitted.Count > 0)
            {
                return null;   // a submitted command; the timer stays armed for this run
            }

            if (_timers.Fired)
            {
                return _timers.Take();
            }

            var seen = _arrivals;
            bool Woken() => _report is not null || _submitted.Count > 0 || _timers.Fired || _arrivals != seen;
            _ = await Workflow.WaitConditionAsync(Woken, NextExpiry() ?? Timeout.InfiniteTimeSpan).ConfigureAwait(true);
        }
    }

    /// <summary>Records the open task once per run token ('run:{token}:{n}') </summary>
    private async Task RecordRun(Frame frame, OpenRun open, RecipeState state)
    {
        if (!string.Equals(_recorded, open.Token.Value, StringComparison.Ordinal))
        {
            _runs++;
            var step = new StepRun(_start!.WorkflowId().Value, frame.State, open.Token.Value); // why: RunStep runs only after RunAsync set _start
            await Workflow.ExecuteActivityAsync(
                (RunStepActivities a) => a.RunStepAsync(step),
                new ActivityOptions
                {
                    ActivityId = string.Create(CultureInfo.InvariantCulture, $"run:{open.Token.Value}:{_runs}"),
                    TaskQueue = StepTaskQueue.For(state, Workflow.Info.TaskQueue),
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                }).ConfigureAwait(true);
            _recorded = open.Token.Value;
        }
    }

    /// <summary>Hands what the loop did with a report to the 'complete' Update that carried it.</summary>
    private void Settle(string? reportId, Reported reported)
    {
        if (reportId is not null)
        {
            _reported[reportId] = reported;
        }
    }

    /// <summary>What a report did, in the words the CLI prints (spec §6.3): '-> checks', 'refused: re-running produce (round 2/3)', 'parked: rounds'.</summary>
    private static string Happened(WorkflowInstance before, WorkflowInstance after, Decision? decision) => (decision, after) switch
    {
        (_, { Suspension: { Kind: SuspensionKind.Park, Reason: { } reason } }) => "parked: " + ReasonName(reason),
        (_, { Suspension.Kind: SuspensionKind.Escalation }) => "escalated",
        (Refused, _) when after.Frame.Run is { } rerun => $"refused: re-running {after.Frame.State} (run {rerun.Token.Value})",
        (Refused, _) => "refused",
        (Absorbed absorbed, _) => "nothing changed: " + absorbed.Reason,
        _ when ReferenceEquals(before, after) => "nothing changed",
        _ => "-> " + after.State,
    };

    private static string ReasonName(ParkReason reason) => reason switch
    {
        ParkReason.Rounds => "rounds",
        ParkReason.NoProgress => "no-progress",
        ParkReason.Budget => "budget",
        ParkReason.NeedsAnswer => "needs-answer",
        ParkReason.Timeout => "timeout",
        ParkReason.SpecConflict => "spec-conflict",
        ParkReason.Launch => "launch",
        ParkReason.NoRuntime or _ => "SND-WF-010",
    };

    /// <summary>A validator's refusal, or a report the loop rejected: the ApplicationFailure the client maps back to its code.</summary>
    private static ApplicationFailureException Refusal(Error error) =>
        new($"{error.Message}", errorType: error.Code, nonRetryable: true);
}
